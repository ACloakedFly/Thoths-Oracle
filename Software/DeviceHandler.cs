using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.IO.Ports;
using ImageMagick;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Drawing.Imaging;
using Contexts;
using Windows.Media;
using System.Diagnostics;
class DeviceHandler{
    public static CoreAudioDevice? playback_device;
    static bool writing_serial = false;
    static bool read_thumb = true;
    static bool oracle_ready = true;
    static bool debug_log = true, debug_console = true;
    static Int32 hash;
    static string Curr = "", album_buffer = "", artist_buffer = "", title_buffer = "", previous_info = "";
    static UInt32 song_duration = 0, song_position = 0;
    static UInt16 reset_pos = 0;
    static string LastString = "";
    static string source = "", info = "info.txt";
    static string logs = "log_", log_dir = "logs\\";
    static SerialPort serialPort = new SerialPort();
    static bool read_uart = true;
    static bool img_exists = false;
    const UInt16 max_attempts = 20;
    static uint serial_error = 0;
    //public static GlobalSystemMediaTransportControlsSessionManager? gsmtcsm;
    public static GlobalSystemMediaTransportControlsSession? gsmtcs;
    public static async Task HandlerSetup(string[] args) {

        if(debug_log)
            DebugLogs();
        serial_setup();
        GeneralSetup();
        var gsmtcsm = await GetSystemMediaTransportControlsSessionManager();
        gsmtcs = gsmtcsm.GetCurrentSession();
        if (gsmtcs != null)
        {
            if (gsmtcs.SourceAppUserModelId.Equals(source))
            {
                var mediaProperties = await GetMediaProperties(gsmtcs);
            }
            else
                gsmtcs = null;
        }
        //playback_device = new CoreAudioController().GetDefaultDevice(deviceType, role);
        Gsmtcsm_Current_Session_Changed(gsmtcsm, null);
        gsmtcsm.CurrentSessionChanged += Gsmtcsm_Current_Session_Changed;


        Console.ReadLine();
    }

    private static void DebugLogs()
    {
        Directory.CreateDirectory(log_dir);
        logs = string.Concat(log_dir, logs, DateTime.Now.Day + "_" + DateTime.Now.Month + "_" + DateTime.Now.Year + "_t_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute + ".txt");
        File.AppendAllText(logs, "\nLog Start:\n");
    }

    private static void WriteLog(string log_text, string? path = null)
    {
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
            Console.WriteLine(log_text);
    }

    private static void GeneralSetup()
    {
        string[] data;
        try
        {
            data = File.ReadAllLines(info);
        }
        catch (FileNotFoundException)
        {
            string[] default_data = { "5", "Default Device", "COM3", "" };
            data = default_data;
            File.AppendAllText("info.txt", "5\nDefault Device\nCOM3\n");
        }
        if (data.Length >= 4)
        {
            source = data[3];
        }
    }

    private static async Task read_from_stream(Windows.Storage.Streams.Buffer buf, IRandomAccessStreamReference stre){
        if(stre == null)
            return;
            try{
                IRandomAccessStreamWithContentType fd = await stre.OpenReadAsync();//IRandomAccessStreamReferenceMethods
                await fd.ReadAsync(buf, buf.Capacity, InputStreamOptions.ReadAhead);
                //Console.WriteLine("wefw");
                read_thumb = true;
            }
            catch(Exception ex){
                WriteLog(ex.ToString());
            }
    }
    private static void Gsmtcsm_Current_Session_Changed(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs? args)
    {
        var session = sender.GetCurrentSession();
        if (session != null)
        {
            WriteLog("Source App changed to: " + session.SourceAppUserModelId);
            if (source == "" || session.SourceAppUserModelId.Equals(source))
            {
                session.MediaPropertiesChanged += S_MediaPropertiesChanged;
                S_MediaPropertiesChanged(session, null);
                session.PlaybackInfoChanged += PlaybackInfoChanged;
                PlaybackInfoChanged(session, null);
                //session.TimelinePropertiesChanged += TimeLineInfoChanged;
                //TimeLineInfoChanged(session, null);
                gsmtcs = session;
            }
            else
            {
                WriteLog("Session does not match");
            }
        }
        else
        {
            WriteLog("No Current seesion");
        }
    }
    private static void TimeLineInfoChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs? args)
    {
        GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = sender.GetTimelineProperties();
        if (timelineProperties != null)
        {
            //Console.WriteLine("Duration: " + timelineProperties.EndTime + " Position: " + timelineProperties.Position.TotalSeconds);
            //WriteLog("Duration: " + timelineProperties.EndTime + " Position: " + timelineProperties.Position.TotalSeconds);
            song_duration = (UInt32)timelineProperties.EndTime.TotalSeconds;
            song_position = (UInt32)timelineProperties.Position.TotalSeconds;
            //Console.WriteLine("Total seconds: " + song_duration);
        }
        else
        {
            song_duration = 0;
            song_position = 0;
        }
    }
    private static void PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs? args)
    {
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = sender.GetPlaybackInfo();
        UInt16 attempts = 0;
        TimeLineInfoChanged(sender, null);
        byte byt = playbackInfo.PlaybackStatus.ToString().Equals("Paused") ? (byte)0 : (byte)1;
        byte[] bytes = BitConverter.GetBytes(song_position).ToArray().Concat(BitConverter.GetBytes(song_duration).ToArray()).ToArray();
        while (!oracle_ready && attempts < max_attempts)
        {
            attempts++;
            Thread.Sleep(400);
            WriteLog("checking from pbi");
        }
        if (attempts >= max_attempts)
            return;
        WriteLog("We playing? " + byt + " we reseting? " + reset_pos);
        Write_Bytes(4, 8, bytes, byt, reset_pos);//reset position when (duration == 0 and reset_pos == 1) || (duration != 0)
        //don't reset position when (duration == 0 and reset pos == 0)
        reset_pos = 0;
    }
    private static async void S_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args){
        GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties = await sender.TryGetMediaPropertiesAsync();
        TimeLineInfoChanged(sender, null);
        if (mediaProperties != null)
        {
            if (mediaProperties.Artist.Length == 0)
                artist_buffer = "No Data";
            else
                artist_buffer = mediaProperties.Artist;

            if (mediaProperties.Title.Length == 0)
                title_buffer = "No Data";
            else
                title_buffer = mediaProperties.Title;

            if (mediaProperties.AlbumTitle.Length == 0)
                album_buffer = "No Data";
            else
                album_buffer = mediaProperties.AlbumTitle;
            Curr = title_buffer + "\n" + album_buffer + "\n" + artist_buffer + "\n";
            if (!Curr.Equals(LastString) && mediaProperties.GetHashCode() != hash)
            {
                LastString = Curr;
                hash = mediaProperties.GetHashCode();
                //PlaybackInfoChangedEventArgs ar;
                //ar = PlaybackInfoChangedEventArgs.FromAbi(0);
                PlaybackInfoChanged(sender, null);
                if (read_thumb && !writing_serial)
                {
                    read_thumb = false;
                    reset_pos = 1;
                    writing_serial = true;
                    await ThumbNailSend(sender);
                    //Write_Bytes(2, (uint)System.Text.Encoding.UTF8.GetByteCount(Curr), System.Text.Encoding.UTF8.GetBytes(Curr), 0, 0);
                    WriteLog("Serial writing complete " + previous_info);
                    writing_serial = false;
                }
            }
            else if (!previous_info.Equals(Curr))
            {
                await Task.Delay(10000);
                if (read_thumb && !writing_serial)
                {
                    read_thumb = false;
                    reset_pos = 1;
                    writing_serial = true;
                    await ThumbNailSend(sender);
                    writing_serial = false;
                }
            }
        }
    }
    private static async Task ThumbNailSend(GlobalSystemMediaTransportControlsSession sender){
        GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties = await sender.TryGetMediaPropertiesAsync();
        WriteLog("Got info");
        if (mediaProperties.Thumbnail == null)
        {
            WriteLog("No thumbnail");
            await Task.Delay(2000);
            mediaProperties = await sender.TryGetMediaPropertiesAsync();
        }
        await Get_Thumbnail(mediaProperties.Thumbnail);
        await Resize();
    }
    private static async Task<GlobalSystemMediaTransportControlsSessionManager> GetSystemMediaTransportControlsSessionManager(){
        return await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    private static async Task<GlobalSystemMediaTransportControlsSessionMediaProperties?> GetMediaProperties(GlobalSystemMediaTransportControlsSession? session){
        if(session == null)
            return null;
        return await session.TryGetMediaPropertiesAsync();
        /*try{
            return await session.TryGetMediaPropertiesAsync();
        }
        catch (System.NullReferenceException){
            return null;
        }*/
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
    private static Task<int> Resize(){
        UInt16 attempts = 0;
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
            read_thumb = true;
        }
        catch (MagickCorruptImageErrorException){
            WriteLog("error insufficient data");
            img_exists = false;
            read_thumb = true;
        }
        previous_info = Curr;
        while (!oracle_ready)
        {
            if (attempts >= max_attempts)
            {
                return Task.FromResult(0);
            }
            attempts++;
            Thread.Sleep(400);
            WriteLog("checking for text ");
        }
        Write_Bytes(3, 0, System.Text.Encoding.UTF8.GetBytes("s"), (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
        //Write_Bytes(3, 0, null, (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
        attempts = 0;
        while (!oracle_ready)
        {
            if (attempts >= max_attempts)
            {
                return Task.FromResult(0);
            }
            attempts++;
            Thread.Sleep(400);
            WriteLog("checking for text ");
        }
        Write_Bytes(2, (uint)System.Text.Encoding.UTF8.GetByteCount(Curr), System.Text.Encoding.UTF8.GetBytes(Curr), 0, 0);
        if(img_exists){
            // Add padding
            WriteLog("image exists");
            int imageSize = Math.Max(img.Width, img.Height);
            img.Extent(imageSize, imageSize, Gravity.Center, MagickColors.Black);
            img.Write("thumby.jpg");
            IPixelCollection<byte> pixels = img.GetPixels();
            byte[]? bytes = pixels.ToByteArray(PixelMapping.RGB);
            bytes = ConvertTo565(bytes);
            if (bytes != null)
            {
                WriteLog("Image Width: " + img.Width + "\nImage height: " + img.Height + "\nImage bytes: " + bytes.Length);
            }
            if(bytes == null || pixels == null)
                return Task.FromResult(0);
            attempts = 0;
            while (!oracle_ready)
            {
                if (attempts >= max_attempts)
                {
                    return Task.FromResult(0);
                }
                attempts++;
                Thread.Sleep(400);
                WriteLog("checking for image ");
            }
            Write_Bytes(1, (uint)bytes.Length, bytes, (ushort)img.Width, (ushort)img.Height);
        }
        return Task.FromResult(0);
    }
    private static void serial_setup(){
        Thread readThread = new Thread(Read);

        serialPort.PortName = "COM3";
        serialPort.BaudRate = 921600;//115200;921600
        serialPort.Parity = Parity.Even;
        serialPort.DataBits = 8;
        serialPort.StopBits = StopBits.Two;
        serialPort.Handshake = Handshake.RequestToSend;
        serialPort.DtrEnable = true;
        //serialPort.DsrHolding
        serialPort.ReadTimeout = 500;
        serialPort.WriteTimeout = 100000;
        try
        {
            serialPort.Open();
            readThread.Start();
            if (serialPort.DsrHolding)
            {
                
            }
            while (!oracle_ready)
            {
                Thread.Sleep(400);
                WriteLog("checking for device ");
            }
            //Write_Bytes(3, 0, System.Text.Encoding.UTF8.GetBytes("s"), (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
            Write_Bytes(3, 0, null, (ushort)((ushort)DateTime.Now.Day + ((byte)DateTime.Now.Month << 8)), (ushort)DateTime.Now.Year, (UInt32)DateTime.Now.TimeOfDay.TotalSeconds);
        }
        catch (FileNotFoundException)
        {
            WriteLog("No such device. Ensure Selected com port is correct and device is plugged in.");
        }
    }
    private static async void Read(){
        int mes;
        byte cmd;
        double vol;
        while(read_uart){
            try
            {
                mes = serialPort.ReadByte();
                string message = Convert.ToChar(mes).ToString();
                string stst = "reg";
                //string message = serialPort.ReadLine();
                //Console.WriteLine(Convert.ToByte(message[0]));
                //Console.Write(message);
                if (mes == 8)
                {
                    stst = serialPort.ReadLine();
                    WriteLog("Serial error: '" + stst + "'");
                    serial_error = 1;
                }
                if (mes == 7)
                {
                    //Console.Write("Serial has acknowledged: ");
                    stst = serialPort.ReadLine();
                    //Console.WriteLine("'" + stst + "'!");
                    WriteLog("Serial has acknowledged: '" + stst + "'!");
                    oracle_ready = true;
                }
                if (mes == 6)
                {
                    stst = serialPort.ReadLine();
                    WriteLog("Serial responded: '" + stst + "'");
                }
                if (mes == '')
                {
                    try
                    {
                        cmd = (byte)serialPort.ReadChar();
                        WriteLog("Command: " + cmd);
                        if (cmd <= 3 && playback_device != null)
                        {
                            vol = await playback_device.GetVolumeAsync();
                            if (cmd == 1 && vol != 0)
                            {
                                await playback_device.SetVolumeAsync(vol - GUI.volume_sens);
                            }
                            else if (cmd == 2 && vol != 100)
                            {
                                await playback_device.SetVolumeAsync(vol + GUI.volume_sens);
                            }
                            else if (cmd == 3)
                            {
                                await playback_device.ToggleMuteAsync();
                            }
                        }
                        else if (cmd > 3 && gsmtcs != null)
                        {
                            if (cmd == 4)
                            {
                                await gsmtcs.TrySkipPreviousAsync();
                            }
                            else if (cmd == 5)
                            {
                                await gsmtcs.TryTogglePlayPauseAsync();
                                WriteLog(gsmtcs.SourceAppUserModelId);
                                //Console.WriteLine("Time is: " + gsmtcs.GetTimelineProperties().Position);
                            }
                            else if (cmd == 6)
                            {
                                await gsmtcs.TrySkipNextAsync();
                            }
                        }
                        if (gsmtcs == null)
                        {
                            WriteLog("GSMTCS is null :(");
                        }
                    }
                    catch (TimeoutException)
                    {

                    }
                }
                else
                {
                    //WriteLog(serialPort.ReadLine());
                }
            }
            catch (TimeoutException)
            {
                //Console.WriteLine("UART read timed out");
            }
        }
    }
    private static void Write_Bytes(byte tag, uint length, byte[]? s, ushort width, ushort height, UInt32 dur = 0){
        if (serial_error != 0)
        {
            WriteLog("Error");
            Thread.Sleep(1000);
            serial_error = 0;
            //oracle_ready = true;
            return;
        }
        if (s == null)
        {
            s = Array.Empty<byte>();
            length = 0;
        }
        oracle_ready = false;
        byte[] bytleng = BitConverter.GetBytes(length);
        byte[] bytwidth = BitConverter.GetBytes(width);
        byte[] bytheight = BitConverter.GetBytes(height);
        byte[] bytdur = BitConverter.GetBytes(dur);
        byte[] header = new byte[12]{tag, bytleng[0], bytleng[1], bytleng[2], bytwidth[0], bytwidth[1], bytheight[0], bytheight[1],
        bytdur[0], bytdur[1], bytdur[2], bytdur[3]};
        byte[] bytes = (tag==3)? header.ToArray() : header.Concat(s.Take((int)length).ToArray()).ToArray();//what the fuck is this madness???
        WriteLog("Total bytes to be sent: " + bytes.Count() +  " Tag: " + tag + " Length: " + length);
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
        }
        catch (IOException)
        {
            WriteLog("Device disconnected, IOEx");
        }
    }
    private static byte[]? ConvertTo565(byte[]? bytes){
        if(bytes == null)
            return null;

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