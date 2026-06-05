using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace Henchman;

public static class Loc
{
    private static Dictionary<string, Dictionary<string, string>> Feature = new();
    private static Dictionary<string, Dictionary<string, string>> FeatureFallback = new();
    private static Dictionary<string, string> General = new();
    private static Dictionary<string, string> GeneralFallback = new();
    private static string ActiveLang = "en";

    public static void Load(string lang)
    {
        ActiveLang = lang;
        LoadDir(lang, out Feature, out General);

        if (lang != "en")
        {
            LoadDir("en", out FeatureFallback, out GeneralFallback);
            ValidateMissing();
        }
    }

    private static void LoadDir(string lang, out Dictionary<string, Dictionary<string, string>> feature, out Dictionary<string, string> general)
    {
        feature = new Dictionary<string, Dictionary<string, string>>();
        general = new Dictionary<string, string>();

        var baseDir = Svc.PluginInterface.AssemblyLocation.Directory!.FullName;
        var dir = Path.Combine(baseDir, "Localization", lang);
        if (!Directory.Exists(dir))
            dir = Path.Combine(baseDir, "Localization", "en");
        if (!Directory.Exists(dir))
            return;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file)) ?? new Dictionary<string, string>();
            if (name == "General")
                general = dict;
            else
                feature[name] = dict;
        }
    }

    private static void ValidateMissing()
    {
        foreach (var (file, enDict) in FeatureFallback)
        {
            if (!Feature.TryGetValue(file, out var activeDict))
            {
                InternalWarning($"[Loc] Missing file [{ActiveLang}]: {file}.json");
                continue;
            }

            foreach (var key in enDict.Keys.Where(k => !activeDict.ContainsKey(k)))
                InternalWarning($"[Loc] Missing [{ActiveLang}] {file}/{key}");
        }

        foreach (var key in GeneralFallback.Keys.Where(k => !General.ContainsKey(k)))
            InternalWarning($"[Loc] Missing [{ActiveLang}] General/{key}");
    }

    public static string G(string key) => General.TryGetValue(key, out var s) ? s :
                                          GeneralFallback.TryGetValue(key, out s) ? s : key;

    public static string F(string typeName, string key) => Feature.TryGetValue(typeName, out var d) && d.TryGetValue(key, out var s) ? s :
                                                           FeatureFallback.TryGetValue(typeName, out d) && d.TryGetValue(key, out s) ? s : key;

    public static string F(string typeName, string key, string defaultValue) => Feature.TryGetValue(typeName, out var d) && d.TryGetValue(key, out var s) ? s :
                                                                                FeatureFallback.TryGetValue(typeName, out d) && d.TryGetValue(key, out s) ? s : defaultValue;
}
