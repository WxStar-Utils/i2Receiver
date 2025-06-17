using System.Runtime.CompilerServices;
using i2Receiver.Communication;

namespace i2Receiver
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "wxstar.dev | IntelliStar 2 Data Receiver - Init..";
            Config config = Config.Load();
            Log.SetLogLevel(Config.config.LogLevel);

            // Create the temp directory if it does not exist 
            if (!Directory.Exists("temp"))
                Directory.CreateDirectory("temp");
            
            await StarApi.CheckStarAuthorization();
            
            var client = new MqttClient();
            
            
            await client.HandleReceivedMessages();
        }
    }
}
