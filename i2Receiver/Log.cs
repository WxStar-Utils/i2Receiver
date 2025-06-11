using System.Drawing;
using Pastel;

namespace i2Receiver;

/// <summary>
/// Implementation of Moon's logging system
/// </summary>
public class Log
{
    private static string PREFIX_DEBUG = " [DEBUG] ";
    private static string PREFIX_INFO = " [INFO] ";
    private static string PREFIX_WARNING = " [WARN] ";
    private static string PREFIX_ERROR = " [ERROR] ";
    
    // Log Colors
    private static Color COLOR_DEBUG = Color.LightSeaGreen;
    private static Color COLOR_INFO = Color.LightSkyBlue;
    private static Color COLOR_WARNING = Color.Yellow;
    private static Color COLOR_ERROR = Color.Firebrick;
    
    private static LogLevel level = LogLevel.Info;
    
    public static string logStartDate = DateTime.Today.ToString("ddMMyyyy");

    private static string GetDate()
    {
        return DateTime.Now.ToString(@"MM/dd/yyyy HH:mm:ss");
    }

    public static void SetLogLevel(string newLevel)
    {
        newLevel = newLevel.ToLower();
        switch (newLevel)
        {
            case "debug": level = LogLevel.Debug; 
                break;
            case "info": level = LogLevel.Info;
                break;
            case "warning": level = LogLevel.Warning;
                break;
        }
    }

    public static void Debug(string str)
    {
        if (level > LogLevel.Debug) return;
        str = GetDate() + PREFIX_DEBUG + str;
        Console.WriteLine(str.Pastel(COLOR_DEBUG));
        WriteLog(str);
    }

    public static void Info(string str)
    {
        if (level > LogLevel.Info) return;
        str = GetDate() + PREFIX_INFO + str;
        Console.WriteLine(str.Pastel(COLOR_INFO));
        WriteLog(str);
    }

    public static void Warning(string str)
    {
        if (level > LogLevel.Warning) return;
        str = GetDate() + PREFIX_WARNING + str;
        Console.WriteLine(str.Pastel(COLOR_WARNING));
        WriteLog(str);
    }

    public static void Error(string str)
    {
        str = GetDate() + PREFIX_ERROR + str;
        Console.WriteLine(str.Pastel(COLOR_ERROR));
        WriteLog(str);
    }

    private static void WriteLog(string str)
    {
        // Create Log Folder 
        if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "logs")))
        {
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
        }

        string fileName = Path.Combine(AppContext.BaseDirectory, "logs", $"MoonRecv-{logStartDate}.log");

        using (StreamWriter sw = File.AppendText(fileName))
        {
            sw.WriteLine(str);
        }
    }


}
internal enum LogLevel
{
    Debug,
    Info,
    Warning
}