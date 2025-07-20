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

class ConfigHandler
{
    public const string default_path = "config.yaml";
    private static readonly DeviceHandler.Oracle_Configuration default_oracle_config = new()
    {
        ComPort = "COM3",
        WriteTimeout = 5000,
        ReadTimeout = 1000,
        ConnectionWait = 500,
        ReConnectionWait = 2000,
        MediaCheck = 500,
        ConfigCheck = 1000,
        OracleReadyWait = 400,
        DisconnectedWait = 4000,
        VolumeSensitivityOptions = new() { 1, 3, 5 },
        VolumeSensitivity = 5,
        PlaybackDevice = "Default Device",
        Speed = 921600,
        MonitoredProgram = new(),
        AlbumArtist = false,
        WallpaperMode = false,
        WallpaperPeriod = 5,
        LogContinuous = false,

    };
    const string default_config = @"
#Configuration file

#Port can be found in the system tray menu or through Device Manager on windows.
ComPort: COM3
#Choose a volume from the list below. If none match what you want, edit or add to the list. This will update the options in the GUI menu too
VolumeSensitivity: 5
VolumeSensitivityOptions:
- 1
- 3
- 5
#Playback device can be selected through the menu. Default will listen to OS for device focus. 
#But if multiple audio devices are used, like mics and multiple speakers, specifying this will force the volume knob to control only that device
PlaybackDevice: Default Device
#Change this if you want the album artist displayed instead of the artist, or vice versa
AlbumArtist: true
#Change which programs Thoth's Oracle listens to.
#If multiple are provided, whichever currently has focus will be used.
#If none are provided, any program displaying media to the OS will be used. These options will get messy if multiple programs are fighting for focus
#I recommend only specifying one program, or programs that won't be run concurrently.
MonitoredProgram:
- MusicBee.exe
- vlc.exe
#Wallpaper mode for cycling through images in Wallpapers folder
WallpaperMode: false
#How long before image changes in minutes
WallpaperPeriod: 5


#Nitty gritty tuning. These values should be good for most circumstances
#Speed is unused
Speed: 9600
WriteTimeout: 5000
ReadTimeout: 1000
ConnectionWait: 500
ReConnectionWait: 2000
MediaCheck: 500
ConfigCheck: 1000
OracleReadyWait: 400
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

    public static DeviceHandler.Oracle_Configuration LoadConfig(string configurationFile = default_path)
    {
        try
        {
            using var input = File.OpenText(configurationFile);
            var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance);

            var deserializerr = deserializerBuilder.Build();

            var result = deserializerr.Deserialize<DeviceHandler.Oracle_Configuration>(input);
            return result;
        }
        catch (Exception ex)
        {
            ExceptionHandler(ex);
        }
        return default_oracle_config;
    }
    public static void SaveConfig(DeviceHandler.Oracle_Configuration config, string configurationFile = default_path)
    {
        try
        {
            var serializerBuilder = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance);
            var serializer = serializerBuilder.Build();
            string yaml = serializer.Serialize(config);
            List<string> yamls = yaml.Split('\n').ToList();
            int line = 0;
            foreach (string s in File.ReadLines(configurationFile))
            {
                if (s == "" || s.StartsWith('#'))
                {
                    yamls.Insert(line, s + "\r");
                }
                line++;
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
    public static void ConfigChangeHandler()
    {
        using var watcher = new FileSystemWatcher("\\");
        watcher.NotifyFilter = NotifyFilters.LastWrite;

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Error += OnError;

        watcher.Filter = default_path;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;

        using var wallpaper_watcher = new FileSystemWatcher("Wallpapers");
        wallpaper_watcher.NotifyFilter = NotifyFilters.LastWrite;

        wallpaper_watcher.Changed += OnWallpapersChanged;
        wallpaper_watcher.Deleted += OnWallpapersChanged;
        wallpaper_watcher.Error += OnError;

        wallpaper_watcher.EnableRaisingEvents = true;

        while (GUI.continue_config)
        {
            Thread.Sleep(1000);
        }
    }
    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        DeviceHandler.WriteLog("Config changed");
        Thread.Sleep(500);
        DeviceHandler.config_changed = true;
    }
    private static void OnError(object sender, ErrorEventArgs e)
    {
        DeviceHandler.WriteLog("FileSystemWatcher error " + e.ToString());
    }
    private static void OnWallpapersChanged(object sender, FileSystemEventArgs e)
    {
        DeviceHandler.WriteLog("Wallpapers changed " + e.ChangeType);
        Thread.Sleep(500);
        DeviceHandler.wallpapers_changed = true;
    }
}