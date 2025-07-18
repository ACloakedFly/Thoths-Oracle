/*
===========================================================================
Copyright (C) 2025 Dominique Negm

This file is part of Thoth's Oracle source code.

Thoth's Oracle source code is free software; you can redistribute it
and/or modify it under the terms of the GNU General Public License as
published by the Free Software Foundation; either version 3 of the License,
or (at your option) any later version.

Thoth's Oracle source code is distributed in the hope that it will be
useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Thoth's Oracle; if not, see <https://www.gnu.org/licenses/>
===========================================================================
*/
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.IO.Ports;
using ImageMagick;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using Contexts;
using System.Timers;
class DeviceHandler{
    private static class ComCodes
    {
        public const byte Image = 1;
        public const byte Text = 2;
        public const byte SystemMsg = 3;
        public const byte DurPos = 4;
        public const byte Input = 5;
        public const byte Status = 6;
        public const byte Idling = 7;
        public const byte Finished = 8;
        public const byte Error = 9;
    }
    private static class InputCodes
    {
        public const byte VolumeDown = 1; 
        public const byte VolumeUp = 2; 
        public const byte Mute = 3; 
        public const byte PreviousTrack = 4; 
        public const byte PlayPause = 5; 
        public const byte NextTrack = 6; 
    }
    public static CoreAudioDevice? playback_device;
    static bool updating_media = false;
    static bool queued_media = false;
    static IRandomAccessStreamReference? queued_thumb_stream;
    static bool oracle_ready = true;
    static bool debug_log = true, debug_console = false;
    class Info_Buffers
    {
        public string Title { get; set; }
        public string Album { get; set; }
        public string Artist { get; set; }
        public Info_Buffers(string ti = "No Data", string al = "No Data", string ar = "No Data")
        {
            Title = ti;
            Album = al;
            Artist = ar;
        }
    }
    static string current_media = "";
    static string captured_media = "";
    static bool first_call = true;
    static bool device_connected = false;
    static bool device_initial_connected = false;
    static UInt32 song_duration = 0, song_position = 0;
    static UInt16 reset_pos = 0;
    static string logs = "log_", log_dir = "logs\\";
    static SerialPort serialPort = new SerialPort();
    public static bool config_changed = false;
    static bool img_exists = false;
    const UInt16 max_attempts = 20;
    static uint serial_error = 0;
    public static GlobalSystemMediaTransportControlsSession? gsmtcs;
    public static GlobalSystemMediaTransportControlsSession? previous_control_session;
    public class Oracle_Configuration
    {
        public string? ComPort { get; set; }
        public ushort VolumeSensitivity { get; set; }
        public List<UInt16>? VolumeSensitivityOptions { get; set; }
        public string? PlaybackDevice { get; set; }
        public bool AlbumArtist { get; set; }
        public List<string>? MonitoredProgram { get; set; }
        public uint Speed { get; set; }
        public ushort WriteTimeout { get; set; }
        public ushort ReadTimeout { get; set; }
        public ushort ConnectionWait { get; set; }
        public ushort ReConnectionWait { get; set; }
        public ushort MediaCheck { get; set; }
        public ushort ConfigCheck { get; set; }
        public ushort OracleReadyWait { get; set; }
        public ushort DisconnectedWait { get; set; }
        public bool LogContinuous { get; set; }

    };
    public static Oracle_Configuration config = new();
    public static Oracle_Configuration old_config = new();

    private static System.Timers.Timer reconnect_timer = new();
    private static System.Timers.Timer connected_timer = new();
    private static System.Timers.Timer media_change_timer = new();
    private static System.Timers.Timer config_timer = new();

    //public static async Task HandlerSetup(string[] args)
    public static async void HandlerSetup()
    {
        if (debug_log)
            DebugLogs();
        GeneralSetup();
        GUI.config_thread.Start();
        GUI.read_thread.Start();
        var gsmtcsm = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        gsmtcs = gsmtcsm.GetCurrentSession();
        if (gsmtcs != null)
        {
            if (config.MonitoredProgram != null)
            {
                if (!config.MonitoredProgram.Contains(gsmtcs.SourceAppUserModelId))
                {
                    gsmtcs = null;
                }
            }
        }
        Gsmtcsm_Current_Session_Changed(gsmtcsm, null);
        gsmtcsm.CurrentSessionChanged += Gsmtcsm_Current_Session_Changed;

        connected_timer = new System.Timers.Timer(config.ConnectionWait);
        connected_timer.Elapsed += OnInitialConnection;
        connected_timer.AutoReset = true;
        connected_timer.Start();

        reconnect_timer = new System.Timers.Timer(config.ReConnectionWait);
        reconnect_timer.Elapsed += OnReConnection;
        reconnect_timer.AutoReset = true;
        reconnect_timer.Start();

        media_change_timer = new System.Timers.Timer(config.MediaCheck);
        media_change_timer.Elapsed += OnMediaCheck;
        media_change_timer.AutoReset = true;
        media_change_timer.Start();


        config_timer = new System.Timers.Timer(config.ConfigCheck);
        config_timer.Elapsed += OnConfigCheck;
        config_timer.AutoReset = true;
        config_timer.Start();

        while (GUI.continue_media)
        {
            Thread.Sleep(100);
        }
        media_change_timer.Stop();
        reconnect_timer.Stop();
        connected_timer.Stop();
        config_timer.Stop();
    }
    private static async void OnInitialConnection(object? source, ElapsedEventArgs args)
    {
        if (!device_initial_connected || gsmtcs == null)
            return;

        device_initial_connected = false;
        await Update_Media(gsmtcs, null);
        connected_timer.Stop();
    }
    private static void OnReConnection(object? source, ElapsedEventArgs args)
    {
        if (device_connected)
            return;
        serial_setup();
        reconnect_timer.Stop();
    }
    private static async void OnMediaCheck(object? source, ElapsedEventArgs args)
    {
        if (gsmtcs == null || captured_media.Equals(current_media))
            return;
        await Update_Media(gsmtcs, null);
    }
    private static void OnConfigCheck(object? source, ElapsedEventArgs args)
    {
        if (!config_changed)
            return;
        config_changed = false;
        GeneralSetup();
    }
    private static void DebugLogs()
    {
        Directory.CreateDirectory(log_dir);
        if (config.LogContinuous)
            logs = string.Concat(log_dir, logs, DateTime.Now.Day + "_" + DateTime.Now.Month + "_" + DateTime.Now.Year + "_t_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute + ".txt");
        else
        {
            logs = string.Concat(log_dir, logs, ".txt");
        }
        File.WriteAllText(logs, "\nLog Start:\n");
    }

    public static void WriteLog(string log_text, bool new_line = true, string? path = null)
    {
        string nl = new_line ? "\n" : "";
        if (debug_log)
        {
            if (path == null)
                path = logs;
            try
            {
                File.AppendAllTextAsync(path, "\n" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "\t\t" + log_text);
            }
            catch (System.IO.IOException)
            {

            }
        }
        if (debug_console)
            Console.Write(log_text + nl);
    }

    public static void GeneralSetup()
    {
        config = new Oracle_Configuration();
        config = ConfigHandler.LoadConfig(ConfigHandler.default_path);
        if (config.PlaybackDevice != null)
        {
            WriteLog("Looking for " + config.PlaybackDevice);
            CoreAudioController coreAudioController = new CoreAudioController();
            playback_device = coreAudioController.GetPlaybackDevices(DeviceState.Active).FirstOrDefault(c => c != null && c.Name == config.PlaybackDevice, coreAudioController.GetDefaultDevice(DeviceType.Playback, Role.Multimedia));
        }
        if (playback_device != null)
            WriteLog("Found device: " + playback_device.Name);
        old_config.ComPort ??= "";
        if (!old_config.ComPort.Equals(config.ComPort))
            serial_setup();
        old_config = config;
    }

    private static async Task read_from_stream(Windows.Storage.Streams.Buffer buf, IRandomAccessStreamReference stre){
        if(stre == null)
            return;
            try{
                IRandomAccessStreamWithContentType fd = await stre.OpenReadAsync();
                await fd.ReadAsync(buf, buf.Capacity, InputStreamOptions.ReadAhead);
            }
            catch(Exception ex){
                WriteLog(ex.ToString());
            }
    }
    private static void Gsmtcsm_Current_Session_Changed(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs? args)
    {
        var session = sender.GetCurrentSession();
        if (session == null)
        {
            WriteLog("No current session");
            return;
        }
        WriteLog("Source App changed to: " + session.SourceAppUserModelId);
        if (config.MonitoredProgram != null)
        {
            if (!config.MonitoredProgram.Contains(session.SourceAppUserModelId, StringComparer.OrdinalIgnoreCase))
            {
                WriteLog("Session does not match selections");
                return;
            }
        }
        WriteLog("Which matches a selection");
        if (previous_control_session != null)
        {
            WriteLog("Removing previous session: " + previous_control_session.SourceAppUserModelId);
            previous_control_session.MediaPropertiesChanged -= MediaChanged;
            previous_control_session.PlaybackInfoChanged -= PlaybackInfoChanged;
        }
        previous_control_session = session;
        session.MediaPropertiesChanged += MediaChanged;
        session.PlaybackInfoChanged += PlaybackInfoChanged;
        gsmtcs = session;
    }
    private static async void PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs? args)
    {
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = sender.GetPlaybackInfo();
        GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = sender.GetTimelineProperties();
        song_duration = (uint)timelineProperties.EndTime.TotalSeconds;
        song_position = (uint)timelineProperties.Position.TotalSeconds;
        byte byt = playbackInfo.PlaybackStatus.ToString().Equals("Paused") ? (byte)0 : (byte)1;
        byte[] bytes = BitConverter.GetBytes(song_position).ToArray().Concat(BitConverter.GetBytes(song_duration).ToArray()).ToArray();
        WriteLog("We playing? " + byt + " we reseting? " + reset_pos);
        await Write_Bytes(ComCodes.DurPos, (uint)bytes.Length, bytes, byt, reset_pos);//reset position when (duration == 0 and reset_pos == 1) || (duration != 0)
        //don't reset position when (duration == 0 and reset pos == 0)
        reset_pos = 0;
    }
    private static async Task<int> Update_Media(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args)
    {
        GlobalSystemMediaTransportControlsSessionMediaProperties media_properties = await sender.TryGetMediaPropertiesAsync();
        if (media_properties == null)
            return 0;
        Info_Buffers info_ = new()
        {
            Title = media_properties.Title.Length > 0 ? media_properties.Title : "No Data",
            Album = media_properties.AlbumTitle.Length > 0 ? media_properties.AlbumTitle : "No Data",
            Artist = media_properties.Artist.Length > 0 ? media_properties.Artist : "No Data"
        };
        info_.Artist = config.AlbumArtist ? media_properties.AlbumArtist : media_properties.Artist;
        if (updating_media)
        {
            queued_media = true;
            queued_thumb_stream = media_properties.Thumbnail;
            captured_media = info_.Title + "\n" + info_.Album + "\n" + info_.Artist + "\n";
            WriteLog("Updating queue to\n" + captured_media);
            return 0;
        }
        else
        {
            queued_media = false;
            updating_media = true;
            current_media = info_.Title + "\n" + info_.Album + "\n" + info_.Artist + "\n";
            if (first_call)
            {
                first_call = false;
                captured_media = current_media;
            }
            WriteLog("We are current\n" + current_media);
        }
        IRandomAccessStreamReference thumb_stream = media_properties.Thumbnail;
        if (queued_media)
        {
            WriteLog("Sending queued media");
            queued_media = false;
            if (queued_thumb_stream != null)
                thumb_stream = queued_thumb_stream;
            current_media = captured_media;
        }
        await Write_Bytes(ComCodes.SystemMsg, 0, System.Text.Encoding.UTF8.GetBytes("s"), (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
        await Write_Bytes(ComCodes.Text, (uint)System.Text.Encoding.UTF8.GetByteCount(current_media), System.Text.Encoding.UTF8.GetBytes(current_media), 0, 0);
        await Get_Thumbnail(thumb_stream);
        await Resize_Thumbnail();
        updating_media = false;
        if (gsmtcs == null)
            return 0;
        PlaybackInfoChanged(gsmtcs, null);
        return 0;
    }

    private static async void MediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args)
    {
        reset_pos = 1;
        await Update_Media(sender, args);
    }
    private static async Task<int> Resize_Thumbnail()
    {
        WriteLog("Resize");
        MagickImage img = new MagickImage();
        try{
            img = new MagickImage("thumb.jpg");
            MagickGeometry size = new MagickGeometry(304, 304);
            size.IgnoreAspectRatio = false;
            img_exists = true;
            img.Resize(size);
        }
        catch (MagickFileOpenErrorException){
            WriteLog("error opening file");
            img_exists = false;
        }
        catch (MagickCorruptImageErrorException){
            WriteLog("error insufficient data");
            img_exists = false;
        }
        if(!img_exists)
            return 0;
        // Add padding
        int imageSize = Math.Max(img.Width, img.Height);
        img.Extent(imageSize, imageSize, Gravity.Center, MagickColors.Black);
        img.Write("thumby.jpg");
        IPixelCollection<byte> pixels = img.GetPixels();
        byte[]? bytes = pixels.ToByteArray(PixelMapping.RGB);
        if (bytes == null)
            return 0;
        bytes = ConvertTo565(bytes);
        await Write_Bytes(ComCodes.Image, (uint)bytes.Length, bytes, (ushort)img.Width, (ushort)img.Height);
        return 0;
    }
    private static async Task Get_Thumbnail(IRandomAccessStreamReference thumby){
        Windows.Storage.Streams.Buffer thumb_buffer = new Windows.Storage.Streams.Buffer(5000000);
        await read_from_stream(thumb_buffer, thumby);

        DataReader read_buffer = DataReader.FromBuffer(thumb_buffer);
        read_buffer.ReadBytes(thumb_buffer.ToArray());

        string path = "thumb.jpg";
        try{
            await File.WriteAllBytesAsync(path, thumb_buffer.ToArray());
        }
        catch(IOException){
            await Task.Delay(200);
            try{
                await File.WriteAllBytesAsync(path, thumb_buffer.ToArray());
            }
            catch(IOException){
                //File still in use, not using it this time bucko
            }
        }
    }
    private static void serial_setup()
    {
        if (serialPort.IsOpen)
        {
            serialPort.Close();
        }
        serialPort.PortName = config.ComPort;
        serialPort.BaudRate = (int)config.Speed;
        serialPort.Parity = Parity.Even;
        serialPort.DataBits = 8;
        serialPort.StopBits = StopBits.Two;
        serialPort.Handshake = Handshake.RequestToSend;
        serialPort.DtrEnable = true;
        serialPort.ReadTimeout = config.ReadTimeout;
        serialPort.WriteTimeout = config.WriteTimeout;
        try
        {
            serialPort.Open();
            device_connected = true;
            Write_Bytes(ComCodes.SystemMsg, 0, null, (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
            device_initial_connected = true;
        }
        catch (FileNotFoundException)
        {
            WriteLog("No such device. Ensure Selected com port is correct and device is plugged in.");
            serialPort.Close();
            //device_connected = false;
            reconnect_timer.Start();
        }
        catch (ArgumentException)
        {
            WriteLog("COM port is not valid. Ensure it is in the format of COM##");
            reconnect_timer.Start();
            //device_connected = false;
        }
    }
    public static async void Read()
    {
        int mes;
        byte cmd;
        double vol;
        while (GUI.continue_read)
        {
            try
            {
                mes = serialPort.ReadByte();
                string message = Convert.ToChar(mes).ToString();
                string stst = "reg";
                if (mes == ComCodes.Error)
                {
                    stst = serialPort.ReadLine();
                    WriteLog("Serial error: '" + stst + "'");
                    serial_error = 1;
                }
                else if (mes == ComCodes.Idling || mes == ComCodes.Finished)
                {
                    stst = serialPort.ReadLine();
                    WriteLog("Serial has acknowledged: '" + stst + "'!");
                    oracle_ready = true;
                }
                else if (mes == ComCodes.Status)
                {
                    stst = serialPort.ReadLine();
                    WriteLog("Serial responded: '" + stst + "'");
                }
                else if (mes == ComCodes.Input)
                {
                    cmd = (byte)serialPort.ReadChar();
                    WriteLog("Command: " + cmd);
                    if (cmd <= InputCodes.Mute && playback_device != null)
                    {
                        vol = await playback_device.GetVolumeAsync();
                        if (cmd == InputCodes.VolumeDown && vol != 0)
                        {
                            await playback_device.SetVolumeAsync(vol - config.VolumeSensitivity);
                        }
                        else if (cmd == InputCodes.VolumeUp && vol != 100)
                        {
                            await playback_device.SetVolumeAsync(vol + config.VolumeSensitivity);
                        }
                        else if (cmd == InputCodes.Mute)
                        {
                            await playback_device.ToggleMuteAsync();
                        }
                    }
                    else if (cmd > InputCodes.Mute && gsmtcs != null)
                    {
                        if (cmd == InputCodes.PreviousTrack)
                        {
                            await gsmtcs.TrySkipPreviousAsync();
                        }
                        else if (cmd == InputCodes.PlayPause)
                        {
                            await gsmtcs.TryTogglePlayPauseAsync();
                            WriteLog(gsmtcs.SourceAppUserModelId);
                            //Console.WriteLine("Time is: " + gsmtcs.GetTimelineProperties().Position);
                        }
                        else if (cmd == InputCodes.NextTrack)
                        {
                            await gsmtcs.TrySkipNextAsync();
                        }
                    }
                    if (gsmtcs == null)
                    {
                        WriteLog("GSMTCS is null :(");
                    }
                }
                else
                {
                    //WriteLog(serialPort.ReadLine());
                }
            }
            catch (TimeoutException)
            {

            }
            catch (OperationCanceledException)
            {
                WriteLog("Device has been disconnected, please reconnect");
                device_connected = false;
                reconnect_timer.Start();
                Thread.Sleep(config.DisconnectedWait);
            }
            catch (InvalidOperationException)
            {
                WriteLog("The port is closed");
                device_connected = false;
                reconnect_timer.Start();
                Thread.Sleep(config.DisconnectedWait);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                WriteLog(ex.ToString());
            }

        }
    }
    private static Task<int> Write_Bytes(byte tag, uint length, byte[]? s, ushort width, ushort height, UInt32 dur = 0)
    {
        if (!device_connected)
            return Task.FromResult(0);
        byte attempts = 0;
        while (!oracle_ready)
        {
            if (attempts >= max_attempts)
            {
                return Task.FromResult(0);
            }
            attempts++;
            Thread.Sleep(config.OracleReadyWait);
            WriteLog("Waiting for Oracle to be ready ");
        }
        if (serial_error != 0)
        {
            serial_error = 0;
            Thread.Sleep(2000);
            return Task.FromResult(0);
        }
        oracle_ready = false;
        if (s == null)
        {
            s = Array.Empty<byte>();
            length = 0;
        }
        byte[] bytleng = BitConverter.GetBytes(length);
        byte[] bytwidth = BitConverter.GetBytes(width);
        byte[] bytheight = BitConverter.GetBytes(height);
        byte[] bytdur = BitConverter.GetBytes(dur);
        byte[] header = new byte[12]{tag, bytleng[0], bytleng[1], bytleng[2], bytwidth[0], bytwidth[1], bytheight[0], bytheight[1],
        bytdur[0], bytdur[1], bytdur[2], bytdur[3]};
        byte[] bytes = (tag == 3) ? header.ToArray() : header.Concat(s.Take((int)length).ToArray()).ToArray();//what the fuck is this madness???
        WriteLog("Total bytes to be sent: " + bytes.Count() + " Tag: " + tag + " Length: " + length);
        if (tag == 2)
        {
            for (int i = 0; i < bytes.Count(); i++)//bytes.count
            {
                //Console.Write(" " + bytes[i].ToString("X2"));//, 0x //bytes[i]
            }
        }
        try
        {
            serialPort.Write(bytes, 0, bytes.Count());
        }
        catch (InvalidOperationException)
        {
            WriteLog("Device disconnected, InvalidOp");
            device_connected = false;
        }
        catch (IOException)
        {
            WriteLog("Device disconnected, IOEx");
            device_connected = false;
        }
        return Task.FromResult(0);
    }
    private static byte[] ConvertTo565(byte[] bytes){
        int byte_count = bytes.Count();
        byte[] rgb565 = new byte[byte_count*2/3];
        int i_565 = 0;
        for(int i = 0; i < byte_count; i+=3){
            rgb565[i_565] = (byte)((bytes[i] & 0xF8) | (bytes[i+1] >> 5));
            rgb565[i_565+1] = (byte)(((bytes[i+1] & 0x1C) << 3) | (bytes[i+2]  >> 3));
            i_565+=2;
        }
        return rgb565;
    }    
}
