using System.Runtime.CompilerServices;
using i2Receiver.Communication;

namespace i2Receiver
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "WxStar Utils | IntelliStar 2 Data Receiver - Init..";
            Config config = Config.Load();
            Log.SetLogLevel(Config.config.LogLevel);

            await StarApi.CheckStarAuthorization();
            
            var client = new MqttClient();
            
            
            await client.HandleReceivedMessages();
        }
    }
}
