using System.Reflection;
using System.Xml.Serialization;
using i2Receiver.Schema;
using WxStarManager;
using WxStarManager.Models;

namespace i2Receiver.Communication;

public class StarApi
{
    public static Api api = new Api(Config.config.StarApiEndpoint);
    public static string unitUuid { get; set; }

    public static async Task CheckStarAuthorization()
    {
        if (!File.Exists(".star_uuid"))
        {
            Log.Debug("No star uuid found, prompting user for self-registration option.");
            Console.WriteLine("The .star_uuid file was not found.");
            Console.WriteLine("Would you like to register this unit? [Y/N]");
            string? response = Console.ReadLine();

            if (response.ToLower() == "y")
            {
                Log.Info("Performing unit self-registration..");
                await StarSelfRegistrationPrompt();
            }
            else if (response.ToLower() == "n" || string.IsNullOrEmpty(response))
            {
                Log.Error("No WxStar UUID, unable to continue.");
                throw new Exception("Star unit UUID not present.");
            }


        }
        unitUuid = await File.ReadAllTextAsync(".star_uuid");
        
        Log.Info($"Unit registered with id {unitUuid}");
        if (Config.config.RefreshUnitLocations)
            await RefreshUnitLocations();
    }

    public static async Task StarSelfRegistrationPrompt()
    {
        WxStarIn newStar = new WxStarIn();
        Console.Write("-------------------------------\n" +
                      "Label (leave blank for none) : ");
        
        string? userStarName = Console.ReadLine();
        newStar.Name = userStarName;
        
        Console.Write("i2 Model [XD,HD,JR] : ");

        string? userStarModel = Console.ReadLine();

        while (string.IsNullOrEmpty(userStarModel))
        {
            Console.WriteLine("Invalid response.");
            userStarModel = Console.ReadLine();
        }

        switch (userStarModel.ToLower())
        {
            case "xd":
                newStar.Model = "i2xd";
                break;
            case "jr":
                newStar.Model = "i2jr";
                break;
            case "hd":
                newStar.Model = "i2xd";
                break;
        }
        
        Console.Write("Local Forecast GFX Package : ");
        string? userGfxPkgLf = Console.ReadLine();
        newStar.GfxPkgLf = userGfxPkgLf;
        
        Console.Write("Lower Display Line GFX Package : ");
        string? userGfxPkgLdl = Console.ReadLine();
        newStar.GfxPkgLdl = userGfxPkgLdl;
        
        Console.Write("\nRegistering new star...\n");
        Console.WriteLine("-------------------------------");
        
        try
        {
            StarInfo newStarInfo = await api.RegisterStar(newStar);
            await File.WriteAllTextAsync(".star_uuid", newStarInfo.StarId);
        }
        catch (Exception e)
        {
            Log.Error("Failed to perform self-registration.");
            throw;
        }
    }

    public async static Task RefreshUnitLocations()
    {
        Log.Info("Refreshing unit locations..");

        List<string> locations = new();

        if (!File.Exists(Config.config.UnitConfig.MpcLocation))
        {
            Log.Warning("Invalid MachineProductConfig location. Locations will not be refreshed.");
            return;
        }

        MachineProductConfig mpc;

        using (var reader = new StreamReader(Config.config.UnitConfig.MpcLocation))
        {
            mpc = (MachineProductConfig)new XmlSerializer(typeof(MachineProductConfig)).Deserialize(reader);
        }
        
        var configLocationKeys = new List<string>
        {
            "PrimaryLocation",
            "NearbyLocation1",
            "NearbyLocation2",
            "NearbyLocation3",
            "NearbyLocation4",
            "NearbyLocation5",
            "NearbyLocation6",
            "NearbyLocation7",
            "NearbyLocation8",
            "MetroMapCity1",
            "MetroMapCity2",
            "MetroMapCity3",
            "MetroMapCity4",
            "MetroMapCity5",
            "MetroMapCity6",
            "MetroMapCity7",
            "MetroMapCity8",
        };
        
        foreach (ConfigItem i in mpc.ConfigDef.ConfigItems.ConfigItem)
        {
            if (configLocationKeys.Contains(i.Key))
            {
                Log.Debug(i.Value);
                if (string.IsNullOrEmpty(i.Value.ToString()))
                {
                    continue;
                }

                try
                {
                    string choppedValue = i.Value.ToString().Split("_")[2];

                    // Avoid duplicate locations from being added to the location list
                    if (locations.Contains(choppedValue))
                    {
                        continue;
                    }

                    locations.Add(choppedValue);
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
        }

        try
        {
            await api.UpdateUnitLocations(unitUuid, locations);
        }
        catch (Exception e)
        {
            Log.Error("Failed to update unit locations.");
            Log.Error(e.Message);
        }
    }
}