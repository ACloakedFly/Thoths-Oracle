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
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NamingConventions;
using Contexts;

public class Oracle_Configuration
{
    public string? ComPort { get; set; }//Must be in COMx format
    public ushort VolumeSensitivity { get; set; }//How many volume points to (in/de)crease by for every encoder position change
    public List<ushort>? VolumeSensitivityOptions { get; set; }//List of options for the menu
    public string? PlaybackDevice { get; set; }//Which speakers to control the volume of
    public bool WallpaperMode { get; set; }//Display images in wallpapers folder or listen to media?
    public ushort WallpaperPeriod { get; set; }//How long to wait before changing image in minutes
    public bool AlbumArtist { get; set; }//Send Album artist or artist property?
    public required List<string> MonitoredProgram { get; set; }//List of programs to listen to for media
    public string? WallpaperTitle { get; set; }
    public string? WallpaperAlbum { get; set; }
    public string? WallpaperArtist { get; set; }
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
class ConfigHandler
{
    public const string default_path = "config.yaml";
    public const string wallpapers_path = "Wallpapers";
    private static readonly Oracle_Configuration default_oracle_config = new()
    {
        ComPort = "COM3",
        VolumeSensitivity = 5,
        VolumeSensitivityOptions = new() { 1, 3, 5 },
        PlaybackDevice = "Default Device",
        WallpaperMode = false,
        WallpaperPeriod = 5,
        AlbumArtist = false,
        MonitoredProgram = new() {  "vlc.exe" },
        WallpaperTitle = "Wallpaper mode",
        WallpaperAlbum = " ",
        WallpaperArtist = " ",
        Speed = 921600,
        WriteTimeout = 5000,
        ReadTimeout = 1000,
        ConnectionWait = 500,
        ReConnectionWait = 2000,
        MediaCheck = 500,
        ConfigCheck = 1000,
        OracleReadyWait = 400,
        DisconnectedWait = 4000,
        LogContinuous = false,

    };
    const string default_config = @"#Configuration file

#Port can be found in the system tray menu or through Device Manager on windows.
ComPort: COM3
#Choose a volume from the list below. If none match what you want, edit or add to the list. This will update the options in the GUI menu too
VolumeSensitivity: 3
VolumeSensitivityOptions:
- 1
- 3
- 5
#Playback device can be selected through the menu. Default will listen to OS for device focus. 
#But if multiple audio devices are used, like mics and multiple speakers, specifying this will force the volume knob to control only that device
PlaybackDevice: Default Device
#Wallpaper mode for cycling through images in Wallpapers folder
WallpaperMode: false
#Non UI Settings
#How long before image changes in minutes
WallpaperPeriod: 5
#Change this if you want the album artist displayed instead of the artist, or vice versa
AlbumArtist: false
#Change which programs Thoth's Oracle listens to. Case insenstive.
#If multiple are provided, their order represents their priority (top is first). Only the highest active program will be used.
#If none are provided or none listed are found, program will not listen to any. This is to prevent some programs that drop their sessions when changing tracks.
#Windows 11 is on some other level. Windows 10 used normal app IDs, but 11 obfuscates it for some reason. 
#If your media source isn't listed below, check out session_IDs.txt in the logs folder for a list of running media sources. Copy and paste the ID into the list below.
MonitoredProgram:
- fireFOX.exe #On windows 11 use 308046B0AF4A39CB
- musicbee.exe
#- vlc.exe
#- SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify

#Display the following text when in wallpaper mode
WallpaperTitle: 'Wallpaper Mode'
WallpaperAlbum: ' '
WallpaperArtist: ' '

#Nitty gritty tuning. These values should be good for most circumstances.
#Speed is unused
Speed: 9600
WriteTimeout: 5000
ReadTimeout: 1000
ConnectionWait: 500
ReConnectionWait: 2000
MediaCheck: 500
ConfigCheck: 1000
OracleReadyWait: 1000
DisconnectedWait: 4000
LogContinuous: false
";
    private static void ExceptionHandler(Exception exception)
    {
        switch (exception)
        {
            case YamlException:
                DeviceHandler.WriteLog(exception.Message);
                if (exception.InnerException != null)
                    DeviceHandler.WriteLog(exception.InnerException.Message);

                break;
            case FileNotFoundException:
                File.AppendAllText(default_path, default_config);
                break;
            default:
                DeviceHandler.WriteLog(exception.ToString());
                break;
        }
    }

    public static Oracle_Configuration LoadConfig(string configurationFile = default_path)
    {
        try
        {
            using var input = File.OpenText(configurationFile);
            var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance);

            var deserializerr = deserializerBuilder.Build();

            var result = deserializerr.Deserialize<Oracle_Configuration>(input);
            return result;
        }
        catch (Exception ex)
        {
            ExceptionHandler(ex);
            DeviceHandler.WriteLog("Failed with exception " + ex.ToString());
        }
        return default_oracle_config;
    }
    public static void SaveConfig(Oracle_Configuration config, string configurationFile = default_path)
    {
        try
        {
            var serializerBuilder = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance);
            var serializer = serializerBuilder.Build();
            string yaml = serializer.Serialize(config);
            List<string> yamls = yaml.Split('\n').ToList();
            int line = 0;
            bool non_ui = false;
            using (StreamReader stream = new(configurationFile))
            {
                string? ss = stream.ReadLine();
                while (ss != null)
                {
                    if (!non_ui && (ss == "" || ss.StartsWith('#')))
                    {
                        yamls.Insert(line, ss + "\r");
                        non_ui = ss.Equals("#Non UI Settings");
                        if (non_ui)
                            yamls.RemoveRange(line + 1, yamls.Count - (line + 1));
                    }
                    else
                        yamls.Add(ss + "\r");
                    line++;
                    ss = stream.ReadLine();
                }
            }
            using var output = new StreamWriter(configurationFile);
            foreach (string s in yamls)
            {
                output.Write(s);
            }
        }
        catch (Exception ex)
        {
            ExceptionHandler(ex);
        }
    }
    private static FileSystemWatcher watcher = new("\\");
    private static FileSystemWatcher wallpaper_watcher = new(wallpapers_path);
    public static void ConfigChangeHandler()
    {
        Directory.CreateDirectory(wallpapers_path);
        watcher.NotifyFilter = NotifyFilters.LastWrite;

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Error += OnError;

        watcher.Filter = default_path;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;

        wallpaper_watcher.NotifyFilter = NotifyFilters.LastWrite;

        wallpaper_watcher.Changed += OnWallpapersChanged;
        wallpaper_watcher.Deleted += OnWallpapersChanged;
        wallpaper_watcher.Error += OnError;

        wallpaper_watcher.EnableRaisingEvents = true;
    }
    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        DeviceHandler.WriteLog("Config changed ");
        Thread.Sleep(500);
        GUI.media_writer_queue.TryEnqueue(DeviceHandler.GeneralSetup);
    }
    private static void OnError(object sender, ErrorEventArgs e)
    {
        DeviceHandler.WriteLog("FileSystemWatcher error " + e.ToString());
    }
    private static void OnWallpapersChanged(object sender, FileSystemEventArgs e)
    {
        DeviceHandler.WriteLog("Wallpapers changed " + e.ChangeType);
        Thread.Sleep(500);
        GUI.media_writer_queue.TryEnqueue(DeviceHandler.NewWallpaper);
    }
}