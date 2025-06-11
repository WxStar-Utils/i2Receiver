using System.Xml.Serialization;

namespace i2Receiver;

[XmlRoot("Config")]
public class Config
{
    [XmlElement] public string LogLevel { get; set; } = "info";
    [XmlElement] public string StarApiEndpoint { get; set; } = "REPLACE_ME";
    [XmlElement] public bool PullNationalData { get; set; } = false;
    [XmlElement] public bool RefreshUnitLocations { get; set; } = true;
    [XmlElement] public UnitConfiguration UnitConfig { get; set; } = new();
    [XmlElement] public MqttClientConfiguration MqttClientConfig { get; set; } = new();

    public static Config config = new Config();

    public static Config Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "i2Receiver.config");
        if (!File.Exists(path))
        {
            config = new Config();

            XmlSerializer newConfig = new XmlSerializer(typeof(Config));
            newConfig.Serialize(File.Create(path), config);

            return config;
        }

        XmlSerializer serializer = new XmlSerializer(typeof(Config));
        using (FileStream fileStream = new FileStream(path, FileMode.Open))
        {
            var deserialized = serializer.Deserialize(fileStream);

            if (deserialized != null && deserialized is Config cfg)
            {
                config = cfg;
                return config;
            }
        }

        return new Config();
    }

    public class UnitConfiguration
    {
        [XmlElement] public string MsgAddress { get; set; } = "224.1.1.77";
        [XmlElement] public string IfAddress { get; set; } = "127.0.0.1";
        [XmlElement] public bool ProcessCues { get; set; } = true;
        [XmlElement] public int RoutineMsgPort { get; set; } = 7787;
        [XmlElement] public int PriorityMsgPort { get; set; } = 7788;

        [XmlElement]
        public string MpcLocation { get; set; } =
            "C:\\Program Files (x86)\\TWC\\i2\\Managed\\Config\\MachineProductCfg.xml";
    }

    public class MqttClientConfiguration
    {
        [XmlElement] public string Host { get; set; } = "REPLACE_ME";
        [XmlElement] public string Username { get; set; } = "REPLACE_ME";
        [XmlElement] public string Password { get; set; } = "REPLACE_ME";
    }
}