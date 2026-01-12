using System.IO;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Newtonsoft.Json;

namespace Henchman;

public abstract class FeatureUI
{
    public abstract string                                    Name         { get; }
    public abstract string                                    Category     { get; }
    public abstract FontAwesomeIcon                           Icon         { get; }
    public abstract Action?                                   Help         { get; }
    public abstract bool                                      LoginNeeded  { get; }
    public virtual  List<(string pluginName, bool mandatory)> Requirements { get; } = new();
    public abstract void                                      Draw();

    protected T? LoadConfig<T>(bool isAccount = true) where T : IConfig? => LoadConfig<T>(Name, isAccount);

    protected unsafe T? LoadConfig<T>(string key, bool isAccount = false) where T : IConfig?
    {
        if (!Svc.ClientState.IsLoggedIn && !isAccount) return default;
        try
        {
            var    defaultConfigDirectory = Svc.PluginInterface.GetPluginConfigDirectory();
            string pluginConfigDirectory;
            if (!isAccount)
            {
                var characterConfigDirectory = Path.Join(defaultConfigDirectory, PlayerState.Instance()->ContentId.ToString());
                pluginConfigDirectory = Path.Join(characterConfigDirectory, SanitizePath(Name));
            }
            else
                pluginConfigDirectory = Path.Join(defaultConfigDirectory, SanitizePath(Name));

            if (!Directory.Exists(pluginConfigDirectory)) Directory.CreateDirectory(pluginConfigDirectory);
            var configFile = Path.Combine(pluginConfigDirectory, key + ".json");
            if (!File.Exists(configFile)) return default;
            var jsonString = File.ReadAllText(configFile);
            return JsonConvert.DeserializeObject<T>(jsonString);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Failed to load config for feature {Name}");
            return default;
        }
    }

    protected void SaveConfig<T>(T config, bool isAccount = true) where T : IConfig?
    {
        SaveConfig(config, Name, isAccount);
    }

    protected unsafe void SaveConfig<T>(T config, string key, bool isAccount = false) where T : IConfig?
    {
        try
        {
            var    defaultConfigDirectory = Svc.PluginInterface.GetPluginConfigDirectory();
            string pluginConfigDirectory;
            if (!isAccount)
            {
                var characterConfigDirectory = Path.Join(defaultConfigDirectory, PlayerState.Instance()->ContentId.ToString());
                pluginConfigDirectory = Path.Join(characterConfigDirectory, SanitizePath(Name));
            }
            else
                pluginConfigDirectory = Path.Join(defaultConfigDirectory, SanitizePath(Name));

            if (!Directory.Exists(pluginConfigDirectory)) Directory.CreateDirectory(pluginConfigDirectory);
            var configFile = Path.Combine(pluginConfigDirectory, key + ".json");
            var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);

            File.WriteAllText(configFile, jsonString);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Feature failed to write config {Name}");
        }
    }
}

public abstract class FeatureUI<TConfig> : FeatureUI where TConfig : IConfig
{
    public abstract TConfig Configuration { get; init; }
}
