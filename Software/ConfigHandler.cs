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
    private static readonly DeviceHandler.Oracle_Configuration default_oracle_config = new DeviceHandler.Oracle_Configuration
    {
        ComPort = "COM3",
        WriteTimeout = 5000,
        ReadTimeout = 1000,
        ConnectionWait = 500,
        ReConnectionWait = 2000,
        MediaCheck = 500,
        ConfigCheck = 5000,
        OracleReadyWait = 400,
        DisconnectedWait = 4000,
        VolumeSensitivityOptions = new() {1, 5, 10},
        VolumeSensitivity = 5,
        PlaybackDevice = "Default Device",
        Speed = 921600,
        MonitoredProgram = new(),
    };
    const string default_config = @"
#Configuration file

#Port can be found in Device Manager. Look for 
ComPort: COM3
#Choose a volume from the list below. If none match what you want, edit or add to the list. This will update the options in the GUI menu too
VolumeSensitivity: 5
VolumeSensitivityOptions:
- 1
- 5
- 10
PlaybackDevice: TOSHIBA-TV

MonitoredProgram:
- MusicBee.exe
- vlc.exe

#Nitty gritty tuning. These values should be good for most circumstances
#Speed is unused
Speed: 9600
WriteTimeout: 5000
ReadTimeout: 1000
ConnectionWait: 500
ReConnectionWait: 2000
MediaCheck: 500
ConfigCheck: 5000
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
            using (var input = File.OpenText(configurationFile))
            {
                var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance);

                var deserializerr = deserializerBuilder.Build();

                var result = deserializerr.Deserialize<DeviceHandler.Oracle_Configuration>(input);
                return result;
            }
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
            using (var output = new StreamWriter(configurationFile))
            {
                foreach (string s in yamls)
                {
                    output.Write(s);
                }
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
        watcher.NotifyFilter = NotifyFilters.Attributes
                             | NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastAccess
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Security
                             | NotifyFilters.Size;

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnDeleted;
        watcher.Error += OnError;

        watcher.Filter = default_path;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
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
    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {

    }
    private static void OnError(object sender, ErrorEventArgs e)
    {

    }
}