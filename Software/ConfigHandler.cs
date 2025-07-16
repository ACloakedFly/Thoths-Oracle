using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NamingConventions;

class ConfigHandler
{
    public const string default_path = "config.yaml";
    private static readonly DeviceHandler.Oracle_Configuration default_oracle_config = new DeviceHandler.Oracle_Configuration
    {
        ComPort = "COM3",
        VolumeSensitivity = 5,
        PlaybackDevice = "Default Device",
        Speed = 921600,
        MonitoredProgram = new(),
    };
    const string default_config = @"
    #Configuration file

    #Inline comments won't be saved
    VolumeSensitivity: 5
    PlaybackDevice: Default Device
    ComPort: COM3

    #But this is cool
    Speed: 921600
    MonitoredProgram:
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

    public static DeviceHandler.Oracle_Configuration LoadConfig(string configurationFile)
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
    public static void SaveConfig(string configurationFile, DeviceHandler.Oracle_Configuration config)
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
        while (true)
        {
            Thread.Sleep(10000);
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