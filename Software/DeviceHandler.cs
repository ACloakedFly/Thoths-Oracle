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
using System.Threading.Tasks;
using System.Text;
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
    private static readonly bool debug_log = true;
    private static readonly bool debug_console = false;

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
    static string empty_media = " \n \n \n";
    static string current_media = "";
    static string captured_media = "";
    static bool first_call = true;
    static bool device_connected = false;
    static bool device_initial_connected = false;
    static uint song_duration = 0, song_position = 0;
    static ushort reset_pos = 0;
    static string logs = "log_", log_dir = "logs\\";
    const string wallpaper_path = "Wallpapers\\"; 
    static SerialPort serialPort = new SerialPort();
    public static bool config_changed = false;
    static bool img_exists = false;
    const ushort max_attempts = 20;
    static uint serial_error = 0;
    public static GlobalSystemMediaTransportControlsSession? gsmtcs;
    public static GlobalSystemMediaTransportControlsSession? previous_control_session;
    public static Mutex log_mutex = new();
    public static readonly ushort log_timeout = 500;
    public class Oracle_Configuration
    {
        public string? ComPort { get; set; }//Must be in COMx format
        public ushort VolumeSensitivity { get; set; }//How many volume points to (in/de)crease by for every encoder position change
        public List<ushort>? VolumeSensitivityOptions { get; set; }//List of options for the menu
        public string? PlaybackDevice { get; set; }//Which speakers to control the volume of
        public bool AlbumArtist { get; set; }//Send Album artist or artist property?
        public List<string>? MonitoredProgram { get; set; }//List of programs to listen to for media
        public bool WallpaperMode { get; set; }//Display images in wallpapers folder or listen to media?
        public ushort WallpaperPeriod { get; set; }//How long to wait before changing image in minutes
        public uint Speed { get; set; }//UART speed. Unused as connection is USB Fullspeed
        public ushort WriteTimeout { get; set; }//Serial write timeout in ms
        public ushort ReadTimeout { get; set; }//Serial read timeout in ms
        public ushort ConnectionWait { get; set; }//How long to wait before polling for new device connection and resetting serial comms
        public ushort ReConnectionWait { get; set; }//How long to wait before polling for reconnection and resend media info
        public ushort MediaCheck { get; set; }//How long to wait before
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
    private static System.Timers.Timer wallpaper_timer = new();
    private static List<string> wallpapers = new();
    private static ushort current_wallpaper = 0;
    private static sbyte set_wallpaper = 0;
    private static Mutex wall_mutex = new();
    private static Mutex reconnect_mutex = new();
    public class BalloonTip
    {
        public string title = "";
        public ushort timeout = 0;
        public ToolTipIcon icon = ToolTipIcon.None;
    }
    private static BalloonTip serial_tip = new();
    private static BalloonTip read_tip = new();
    private static readonly ushort serial_tip_timeout = 2000;
    private static Exception prev_write_exception = new();
    private static Exception prev_read_exception = new();
    public static async void HandlerSetup()
    {
        wallpapers = Directory.GetFiles(wallpaper_path).ToList();
        if (debug_log)
            DebugLogs();
        GeneralSetup();
        GUI.read_thread.Start();
        GUI.config_thread.Start();
        GlobalSystemMediaTransportControlsSessionManager gsmtcsm = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        gsmtcs = gsmtcsm.GetCurrentSession();
        if (gsmtcs != null && config.MonitoredProgram != null)
        {
            if (!config.MonitoredProgram.Contains(gsmtcs.SourceAppUserModelId, StringComparer.OrdinalIgnoreCase))
            {
                gsmtcs = null;
            }
        }
        Gsmtcsm_Current_Session_Changed(gsmtcsm, null);
        gsmtcsm.CurrentSessionChanged += Gsmtcsm_Current_Session_Changed;

        media_change_timer = new System.Timers.Timer(config.MediaCheck);
        media_change_timer.Elapsed += OnMediaCheck;
        media_change_timer.AutoReset = true;

        wallpaper_timer = new System.Timers.Timer(config.WallpaperPeriod * 60 * 1000);
        wallpaper_timer.Elapsed += WallpaperTimer;
        wallpaper_timer.AutoReset = true;

        if (!config.WallpaperMode)
        {
            media_change_timer.Start();
        }
        else
        {
            wallpaper_timer.Start();
        }
        connected_timer = new System.Timers.Timer(config.ConnectionWait);
        connected_timer.Elapsed += OnInitialConnection;
        connected_timer.AutoReset = true;
        connected_timer.Start();

        reconnect_timer = new System.Timers.Timer(config.ReConnectionWait);
        reconnect_timer.Elapsed += OnReConnection;
        reconnect_timer.AutoReset = true;
        if(!device_connected)
            reconnect_timer.Start();

        config_timer = new System.Timers.Timer(config.ConfigCheck);
        config_timer.Elapsed += OnConfigCheck;
        config_timer.AutoReset = true;
        config_timer.Start();

        while (GUI.continue_media)
        {
            Thread.Sleep(100);
            if (!wall_mutex.WaitOne(10))
                continue;
            if (set_wallpaper == 0)
            {
                wall_mutex.ReleaseMutex();
                continue;
            }
            else if (set_wallpaper == 2)
            {
                set_wallpaper = 0;
                wall_mutex.ReleaseMutex();
                OnWallpaperChange(0);
                continue;
            }
            OnWallpaperChange(set_wallpaper);
            set_wallpaper = 0;
            wall_mutex.ReleaseMutex();
        }
        media_change_timer.Stop();
        reconnect_timer.Stop();
        connected_timer.Stop();
        config_timer.Stop();
    }
    private static void WallpaperTimer(object? source, ElapsedEventArgs args)
    {
        OnWallpaperChange(1);
    }
    private static void OnWallpaperChange(sbyte direction)
    {
        if (direction != 0)
            current_wallpaper += (ushort)direction;
        if(wallpapers.Count != 0)
            current_wallpaper %= (ushort)wallpapers.Count;
        MagickImage img = new(wallpapers[current_wallpaper]);
        img.Write("thumb.jpg", MagickFormat.Jpg);
        Write_Bytes(ComCodes.DurPos, (uint)8, new byte[8], 0, 1);//reset position when (duration == 0 and reset_pos == 1) || (duration != 0)
        Write_Bytes(ComCodes.Text, (uint)System.Text.Encoding.UTF8.GetByteCount(empty_media), System.Text.Encoding.UTF8.GetBytes(empty_media), 0, 0);
        Resize_Thumbnail();
        return;
    }
    private static async void OnInitialConnection(object? source, ElapsedEventArgs args)
    {
        if (!device_initial_connected || gsmtcs == null || config.WallpaperMode)
            return;

        device_initial_connected = false;
        Console.WriteLine("Initial connection!");
        await Update_Media(gsmtcs, null);
        connected_timer.Stop();
    }
    private static void OnReConnection(object? source, ElapsedEventArgs args)
    {
        //Console.WriteLine("Waiting for reconnection? " + device_connected);
        if (device_connected)
            return;
        SerialSetup();

    }
    private static async void OnMediaCheck(object? source, ElapsedEventArgs args)
    {
        if (gsmtcs == null || captured_media.Equals(current_media) || config.WallpaperMode)
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

    public static void WriteLog(string log_text, bool new_line = true, string? path = null, BalloonTip? tip = null)
    {
        if (!log_mutex.WaitOne(log_timeout))
            return;
        string nl = new_line ? "\n" : "";
        if (debug_log)
        {
            path ??= logs;
            try
            {
                if (new_line)
                    File.AppendAllTextAsync(path, "\n" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "\t\t" + log_text);
                else
                    File.AppendAllTextAsync(path, log_text);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Dang " + ex.ToString());
            }
        }
        if (debug_console)
            Console.Write(log_text + nl);
        if (tip != null)
        {
            GUI.notifyIcon.ShowBalloonTip(tip.timeout, tip.title, log_text, tip.icon);
        }
        log_mutex.ReleaseMutex();
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
            SerialSetup();
        old_config = config;
        if (!wall_mutex.WaitOne(1000))
            return;
        if (config.WallpaperMode)
        {
            set_wallpaper = 2;
            wallpaper_timer.Interval = config.WallpaperPeriod * 60 * 1000;
            wallpaper_timer.Start();
            media_change_timer.Stop();
        }
        else
        {
            media_change_timer.Interval = config.MediaCheck;
            current_media = "";
            wallpaper_timer.Stop();
            media_change_timer.Start();
        }
        wall_mutex.ReleaseMutex();
    }

    private static void Gsmtcsm_Current_Session_Changed(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs? args)
    {
        var session = sender.GetCurrentSession();
        if (session == null)
        {
            WriteLog("No current media session");
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
    private static void PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs? args)
    {
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = sender.GetPlaybackInfo();
        GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = sender.GetTimelineProperties();
        song_duration = (uint)timelineProperties.EndTime.TotalSeconds;
        song_position = (uint)timelineProperties.Position.TotalSeconds;
        byte byt = playbackInfo.PlaybackStatus.ToString().Equals("Paused") ? (byte)0 : (byte)1;
        byte[] bytes = BitConverter.GetBytes(song_position).ToArray().Concat(BitConverter.GetBytes(song_duration).ToArray()).ToArray();
        WriteLog("We playing? " + byt + " we resetting? " + reset_pos);
        Write_Bytes(ComCodes.DurPos, (uint)bytes.Length, bytes, byt, reset_pos);//reset position when (duration == 0 and reset_pos == 1) || (duration != 0)
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
        Write_Bytes(ComCodes.SystemMsg, 0, System.Text.Encoding.UTF8.GetBytes("s"), (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (uint)DateTime.Now.TimeOfDay.TotalSeconds);
        Write_Bytes(ComCodes.Text, (uint)System.Text.Encoding.UTF8.GetByteCount(current_media), System.Text.Encoding.UTF8.GetBytes(current_media), 0, 0);
        Get_Thumbnail(thumb_stream);
        Resize_Thumbnail();
        updating_media = false;
        if (gsmtcs == null)
            return 0;
        PlaybackInfoChanged(gsmtcs, null);
        return 0;
    }

    private static async void MediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args)
    {
        if (config.WallpaperMode)
            return;
        reset_pos = 1;
        await Update_Media(sender, args);
    }
    private static void Resize_Thumbnail()
    {
        WriteLog("Resize");
        MagickImage img = new();
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
            return;
        // Add padding
        int imageSize = Math.Max(img.Width, img.Height);
        img.Extent(imageSize, imageSize, Gravity.Center, MagickColors.Black);
        img.Write("thumby.jpg");
        IPixelCollection<byte> pixels = img.GetPixels();
        byte[]? bytes = pixels.ToByteArray(PixelMapping.RGB);
        if (bytes == null)
            return;
        bytes = ConvertTo565(bytes);
        Write_Bytes(ComCodes.Image, (uint)bytes.Length, bytes, (ushort)img.Width, (ushort)img.Height);
        return;
    }
    private static async Task Read_from_stream(Windows.Storage.Streams.Buffer buf, IRandomAccessStreamReference stre){
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
    private static async void Get_Thumbnail(IRandomAccessStreamReference thumby){
        Windows.Storage.Streams.Buffer thumb_buffer = new(5000000);
        await Read_from_stream(thumb_buffer, thumby);

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
    private static void SerialExceptionHandler(Exception exception)
    {
        if (!reconnect_mutex.WaitOne(500))
            return;
        if (reconnect_timer.Enabled && prev_write_exception.GetType() == exception.GetType())
        {
            reconnect_mutex.ReleaseMutex();
            return;
        }
        reconnect_timer.Start();
        prev_write_exception = exception;
        serial_tip.icon = ToolTipIcon.Warning;
        serial_tip.timeout = serial_tip_timeout;
        device_connected = false;
        string log_message;
        switch (exception)
        {
            case FileNotFoundException:
                serial_tip.title = "Oracle Not Found";
                log_message = "No Oracle on that com port. Ensure Selected com port is correct and Oracle is plugged in.";
                WriteLog(log_message, true, null, serial_tip);
                break;
            case ArgumentException:
                serial_tip.title = "Invalid COM Port";
                log_message = "Ensure it is in the format of COM##";
                WriteLog(log_message, true, null, serial_tip);
                break;
            case UnauthorizedAccessException:
                serial_tip.title = "Access Denied";
                log_message = "Access to " + serialPort.PortName + " denied. Ensure only one program is attempting communication with the Oracle";
                WriteLog(log_message, true, null, serial_tip);
                break;
            case TimeoutException:
                log_message = "Timeout";
                WriteLog(log_message);
                break;
            case OperationCanceledException:
                serial_tip.title = "Oracle Disconnected";
                log_message = "Thoth's Oracle has been disconnected, please reconnect.";
                WriteLog(log_message, true, null, serial_tip);
                Thread.Sleep(config.DisconnectedWait);
                break;
            case InvalidOperationException:
                serial_tip.title = "Com Port Closed";
                log_message = "The port is closed or busy from another program.";
                WriteLog(log_message, true, null, serial_tip);
                Thread.Sleep(config.DisconnectedWait);
                break;
            default:
                WriteLog(exception.Message);
                break;
        }
        reconnect_mutex.ReleaseMutex();
    }
    private static void SerialSetup()
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
        Console.WriteLine("Attempting connection");
        try
        {
            serialPort.Open();
            device_connected = true;
            Write_Bytes(ComCodes.SystemMsg, 0, null, (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (uint)DateTime.Now.TimeOfDay.TotalSeconds);
            device_initial_connected = true;
            connected_timer.Start();
            if (!reconnect_mutex.WaitOne(5000))
                return;
            reconnect_timer.Stop();
            reconnect_mutex.ReleaseMutex();
            serial_tip.icon = ToolTipIcon.Info;
            serial_tip.timeout = serial_tip_timeout;
            serial_tip.title = "Thoth has connected to Oracle";
            WriteLog("Oracle found and communications have begun!", true, null, serial_tip);
        }
        catch (FileNotFoundException ex)
        {
            if (!reconnect_mutex.WaitOne(500))
                return;
            if (reconnect_timer.Enabled && prev_write_exception.GetType() == ex.GetType())
                return;
            prev_write_exception = ex;
            serial_tip.icon = ToolTipIcon.Warning;
            serial_tip.timeout = serial_tip_timeout;
            serial_tip.title = "Oracle Not Found";
            WriteLog("No Oracle on that com port. Ensure Selected com port is correct and Oracle is plugged in.", true, null, serial_tip);
            serialPort.Close();
            device_connected = false;
            reconnect_timer.Start();
            reconnect_mutex.ReleaseMutex();
        }
        catch (ArgumentException ex)
        {
            if (!reconnect_mutex.WaitOne(500))
                return;
            if (reconnect_timer.Enabled && prev_write_exception.GetType() == ex.GetType())
                return;
            prev_write_exception = ex;
            serial_tip.icon = ToolTipIcon.Warning;
            serial_tip.timeout = serial_tip_timeout;
            serial_tip.title = "Invalid COM Port";
            WriteLog("Ensure it is in the format of COM##", true, null, serial_tip);
            reconnect_timer.Start();
            reconnect_mutex.ReleaseMutex();
            device_connected = false;
        }
        catch (UnauthorizedAccessException ex)
        {
            if (!reconnect_mutex.WaitOne(500))
                return;
            if (reconnect_timer.Enabled && prev_write_exception.GetType() == ex.GetType())
                return;
            prev_write_exception = ex;
            serial_tip.icon = ToolTipIcon.Warning;
            serial_tip.timeout = serial_tip_timeout;
            serial_tip.title = "Access Denied";
            WriteLog("Access to " + serialPort.PortName + " denied. Ensure only one program is attempting communication with the Oracle", true, null, serial_tip);
            device_connected = false;
            reconnect_timer.Start();
            reconnect_mutex.ReleaseMutex();
        }
    }

    public static void Read()
    {
        string oracle_message = "";
        int next_byte;
        int code = -1;
        int command = -1;
        while (GUI.continue_read)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    WriteLog("Not connected");
                    Thread.Sleep(config.DisconnectedWait);
                    if (!reconnect_mutex.WaitOne(500))
                        continue;
                    reconnect_timer.Start();
                    reconnect_mutex.ReleaseMutex();
                    continue;
                }
                next_byte = serialPort.ReadChar();
                if (next_byte == 0)
                    continue;
                if (next_byte < 10)
                {
                    if (code < 0)
                        code = next_byte;
                    else
                        command = next_byte;
                }
                else if (next_byte != 10)
                    oracle_message += Convert.ToChar(next_byte);
                else
                {
                    DecodeRead(oracle_message, code, command);
                    oracle_message = "";
                    code = -1;
                    command = -1;
                }
            }
            catch (TimeoutException)
            {
                WriteLog("Timeout");
            }
            catch (OperationCanceledException ex)
            {
                if (!reconnect_mutex.WaitOne(500))
                    continue;
                if (reconnect_timer.Enabled && ex.GetType() == prev_read_exception.GetType())
                    continue;
                prev_read_exception = ex;
                read_tip.title = "Oracle Disconnected";
                read_tip.icon = ToolTipIcon.Warning;
                read_tip.timeout = serial_tip_timeout;
                WriteLog("Thoth's Oracle has been disconnected, please reconnect.", true, null, read_tip);
                device_connected = false;
                reconnect_timer.Start();
                reconnect_mutex.ReleaseMutex();
                Thread.Sleep(config.DisconnectedWait);
            }
            catch (InvalidOperationException ex)
            {
                WriteLog("The port is closed or busy from another program.");//, true, null, read_tip);
                if (!reconnect_mutex.WaitOne(500))
                    continue;
                if (reconnect_timer.Enabled && ex.GetType() == prev_read_exception.GetType())
                    continue;
                prev_read_exception = ex;
                read_tip.title = "Com Port Closed";
                read_tip.icon = ToolTipIcon.Warning;
                read_tip.timeout = serial_tip_timeout;
                WriteLog("The port is closed or busy from another program.");//, true, null, read_tip);
                device_connected = false;
                reconnect_timer.Start();
                reconnect_mutex.ReleaseMutex();
                Thread.Sleep(config.DisconnectedWait);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                WriteLog(ex.ToString());
            }
        }
    }

    public static async void DecodeRead(string oracle_message, int code, int cmd)
    {
        double vol;
        WriteLog("Code: " + code + " Command: " + cmd + " Message: " + oracle_message);
        if (code == ComCodes.Error)
        {
            serial_error = 1;
            WriteLog("Code: " + code + " Command: " + cmd + " Message: " + oracle_message);
        }
        else if (code == ComCodes.Idling || code == ComCodes.Finished)
        {
            WriteLog("Serial has acknowledged: '" + oracle_message + "'!");
            oracle_ready = true;
        }
        else if (code == ComCodes.Status)
        {
            WriteLog("Serial responded: '" + oracle_message + "'");
        }
        else if (code == ComCodes.Input)
        {
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
            else if (cmd > InputCodes.Mute)
            {
                if (config.WallpaperMode && wall_mutex.WaitOne(1))
                {
                    set_wallpaper = (sbyte)((sbyte)cmd - InputCodes.PlayPause);
                    if (set_wallpaper == 0)
                    {
                        if (!wallpaper_timer.Enabled)
                            wallpaper_timer.Start();
                        else
                            wallpaper_timer.Stop();
                    }
                    wall_mutex.ReleaseMutex();
                }
                else if (!config.WallpaperMode && gsmtcs != null)
                {
                    if (cmd == InputCodes.PreviousTrack)
                    {
                        await gsmtcs.TrySkipPreviousAsync();
                    }
                    else if (cmd == InputCodes.PlayPause)
                    {
                        await gsmtcs.TryTogglePlayPauseAsync();
                        WriteLog(gsmtcs.SourceAppUserModelId);
                    }
                    else if (cmd == InputCodes.NextTrack)
                    {
                        await gsmtcs.TrySkipNextAsync();
                    }
                }
            }
            if (gsmtcs == null && !config.WallpaperMode)
            {
                WriteLog("GSMTCS is null :(");
            }
        }
        else
        {
            //WriteLog(serialPort.ReadLine());
        }
    }
    private static void Write_Bytes(byte tag, uint length, byte[]? data, ushort width, ushort height, uint dur = 0)
    {
        if (!device_connected)
            return;
        byte attempts = 0;
        while (!oracle_ready)
        {
            if (attempts >= max_attempts)
            {
                return;
            }
            attempts++;
            Thread.Sleep(config.OracleReadyWait);
            WriteLog("Waiting for Oracle to be ready ");
        }
        if (serial_error != 0)
        {
            serial_error = 0;
            Thread.Sleep(2000);
            return;
        }
        oracle_ready = false;
        if (data == null)
        {
            data = Array.Empty<byte>();
            length = 0;
        }
        byte[] bytleng = BitConverter.GetBytes(length);
        byte[] bytwidth = BitConverter.GetBytes(width);
        byte[] bytheight = BitConverter.GetBytes(height);
        byte[] bytdur = BitConverter.GetBytes(dur);
        byte[] header = new byte[12]{tag, bytleng[0], bytleng[1], bytleng[2], bytwidth[0], bytwidth[1], bytheight[0], bytheight[1],
        bytdur[0], bytdur[1], bytdur[2], bytdur[3]};
        byte[] bytes = (tag == 3) ? header.ToArray() : header.Concat(data.Take((int)length).ToArray()).ToArray();//what the fuck is this madness???
        WriteLog("Total bytes to be sent: " + bytes.Length + " Tag: " + tag + " Length: " + length);
        if (tag == 2)
        {
            for (int i = 0; i < bytes.Length; i++)//bytes.count
            {
                //Console.Write(" " + bytes[i].ToString("X2"));//, 0x //bytes[i]
            }
        }
        try
        {
            serialPort.Write(bytes, 0, bytes.Length);
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
    }
    private static byte[] ConvertTo565(byte[] bytes){
        int byte_count = bytes.Length;
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
