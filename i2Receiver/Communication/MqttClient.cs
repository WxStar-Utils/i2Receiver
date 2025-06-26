using System.IO.Compression;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using System.Xml.Linq;
using i2Receiver.Schema;

namespace i2Receiver.Communication
{
    public class MqttClient
    {
        private string tempDir = Path.Combine(AppContext.BaseDirectory, "temp");
        
        private UDPSender routineSender = new UDPSender(
            Config.config.UnitConfig.MsgAddress,
            Config.config.UnitConfig.RoutineMsgPort,
            Config.config.UnitConfig.IfAddress);
        
        private UDPSender prioritySender = new UDPSender(
            Config.config.UnitConfig.MsgAddress,
            Config.config.UnitConfig.PriorityMsgPort,
            Config.config.UnitConfig.IfAddress);


        private static string _host { get; set; }
        private static string _username { get; set; }
        private static string _password { get; set; }

        public async Task HandleReceivedMessages()
        {
            var mqttFactory = new MqttFactory();

            _host = Config.config.MqttClientConfig.Host;
            _username = Config.config.MqttClientConfig.Username;
            _password = Config.config.MqttClientConfig.Password;

            using (var mqttClient = mqttFactory.CreateMqttClient())
            {
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithWebSocketServer(_host)
                    .WithCredentials(_username, _password)
                    .WithCleanSession()
                    //.WithTls()  // The MQTT broker should be running as https
                    .Build();

                mqttClient.ApplicationMessageReceivedAsync += async e =>
                {
                    string payloadMessage = e.ApplicationMessage.ConvertPayloadToString();

                    Log.Debug($"Received new message from {e.ApplicationMessage.Topic}");
                    
                    if (e.ApplicationMessage.Topic == "wxstar/data/i2" || 
                        e.ApplicationMessage.Topic == "wxstar/data/national/i2" || 
                        e.ApplicationMessage.Topic == "wxstar/data/i2/priority" || 
                        e.ApplicationMessage.Topic == "wxstar/heartbeat")
                        await I2MsgHandler(payloadMessage, e.ApplicationMessage.Topic);

                    if (e.ApplicationMessage.Topic == "wxstar/data/i2/radar")
                        await RadarFrameHandler(payloadMessage);

                    if (e.ApplicationMessage.Topic == "wxstar/cues" && Config.config.UnitConfig.ProcessCues)
                        await CueHandler(payloadMessage);
                };


                // Switch subscribed channels based off of what's set in the configuration file
                MqttClientSubscribeOptionsBuilder optionsBuilder = new();
                MqttClientSubscribeOptions options = new();

                if (Config.config.PullNationalData)
                {
                    options = optionsBuilder
                        .WithTopicFilter("wxstar/data/national/i2")
                        .WithTopicFilter("wxstar/data/i2/priority")
                        .WithTopicFilter("wxstar/heartbeat")
                        .Build();
                }
                else
                {
                    options = optionsBuilder
                        .WithTopicFilter("wxstar/data/i2")
                        .WithTopicFilter("wxstar/data/i2/priority")
                        .WithTopicFilter("wxstar/data/i2/radar")
                        .WithTopicFilter("wxstar/heartbeat")
                        .WithTopicFilter("wxstar/cues")
                        .Build();
                }


                await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                Log.Info($"Connected to MQTT broker @ {_host}");
                Console.Title = $"WxStar Utils | IntelliStar 2 Data Receiver - Connected to {_host}";

                await mqttClient.SubscribeAsync(options, CancellationToken.None);

                Console.ReadLine();
            }
        }

        
        public async Task I2MsgHandler(string payload, string topic)
        {
            CmdMessage? message = JsonConvert.DeserializeObject<CmdMessage>(payload);

            if (message == null)
            {
                Log.Warning("Failed to deserialize i2m.");
                return;
            }

            
            // Send only a command if no data string is present in the message.
            if (message.Data == null)
            {
                Log.Info($"Command received: {message.Command}");
                routineSender.SendCommand(message.Command);
                return;
            }
            
            Log.Debug($"PROCESSING I2M / {topic}");
            
            
            string newFile = Path.Combine(tempDir, Guid.NewGuid().ToString());
            string data = ToProperXml(message.Data.Replace("'", "\""));
            await File.WriteAllTextAsync(newFile, data);
            
            int dataFileSize = Encoding.UTF8.GetByteCount(data);

            if (topic == "wxstar/data/i2" || topic == "wxstar/data/national/i2")
            {
                Log.Info($"Stored new i2m ({dataFileSize} bytes)\nCommand: {message.Command}");
                routineSender.SendFile(newFile, message.Command);
            }

            if (topic == "wxstar/data/i2/priority")
            {
                Log.Warning($"Priority i2m stored ({dataFileSize} bytes)\nCommand: {message.Command}");
                prioritySender.SendFile(newFile, message.Command);
            }
        }

        
        public async Task CueHandler(string payload)
        {
            CueCommand? command = JsonConvert.DeserializeObject<CueCommand>(payload);

            if (command == null)
            {
                Log.Warning("Failed to deserialize presentation cue.");
                return;
            }

            if (command.StartTime != null)
            {
                Log.Info($"Cue {command.CueId} cued to run at {command.StartTime}");
                routineSender.SendCommand($"runPres(PresentationId={command.CueId},StartTime={command.StartTime})");
                return;
            }

            if (command.Cues == null && command.StartTime == null)
            {
                Log.Warning("Malformed or otherwise unusable presentation cue received.");
                return;
            }
            
            foreach (CueListObject cue in command.Cues)
            {
                if (cue.StarUuid != StarApi.unitUuid)
                    continue;
                
                Log.Info($"Presentation {command.CueId} loaded using {cue.Flavor} for {cue.Duration} frames.");
                routineSender.SendCommand($"loadPres(PresentationId={command.CueId},Flavor={cue.Flavor},Duration={cue.Duration},VideoBehind=000)");
                return;
            }
        }

        public async Task RadarFrameHandler(string payload)
        {
            RadarFrame? frameInfo = JsonConvert.DeserializeObject<RadarFrame>(payload);

            if (frameInfo == null)
            {
                Log.Warning("Failed to deserialize radar frame data.");
                return;
            }

            if (!Directory.Exists("temp/frames"))
                Directory.CreateDirectory("temp/frames");
            
            // Create gzip file from the base64 frame data
            var base64EncodedBytes = Convert.FromBase64String(frameInfo.FrameData);

            await File.WriteAllBytesAsync($"temp/frames/{frameInfo.Filename}.gz", base64EncodedBytes);
            
            // Decompress the gzip archive back down to the base .tiff file
            using (FileStream compressedFrame = File.OpenRead($"temp/frames/{frameInfo.Filename}.gz"))
            {
                using (FileStream decompressedFrame = File.Create($"temp/frames/{frameInfo.Filename}"))
                {

                    using (GZipStream decompressionStream = new GZipStream(compressedFrame, CompressionMode.Decompress))
                    {
                        await decompressionStream.CopyToAsync(decompressedFrame);
                    }
                }
            }
            Log.Info($"Processed new radar frame for timestamp {frameInfo.Timestamp}");

            File.Delete($"temp/frames/{frameInfo.Filename}.gz");
            
            prioritySender.SendFile($"temp/frames/{frameInfo.Filename}",
                $"storePriorityImage(FileExtension=.tiff,Location=US,ImagesType=Radar,IssueTime='{frameInfo.Timestamp}')");
            
            File.Delete($"temp/frames/{frameInfo.Filename}");
        }

        
        public string ToProperXml(string data)
        {
            try
            {
                XDocument document = XDocument.Parse(data);
                return document.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return data;
            }
        }
    }
}
