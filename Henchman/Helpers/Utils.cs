using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons.GameHelpers;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Henchman.Helpers;

public static class Utils
{
    public static readonly JsonSerializerOptions EnumAsStringOptions = new()
                                                                       {
                                                                               Converters = { new JsonStringEnumConverter() }
                                                                       };

    internal static bool IsPluginBusy => Running;

    internal static List<Vector3> SortListByDistance(List<Vector3> pointList)
    {
        double DistanceSquaredTo(Vector3 main, Vector3 other)
        {
            double dx = main.X - other.X;
            double dy = main.Y - other.Y;
            double dz = main.Z - other.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        var path   = new List<Vector3> { Player.Position };
        var points = pointList.ToList();

        while (points.Count > 0)
        {
            var last = path[^1];
            var nearest = points
                         .OrderBy(p => DistanceSquaredTo(p, last))
                         .First();

            path.Add(nearest);
            points.Remove(nearest);
        }

        path.RemoveAt(0);

        return path;
    }

    internal static T LoadEmbeddedResource<T>(string resourceName, Func<Stream, T> converter)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");

            return converter(stream);
        }
    }

    internal static T? ReadEmbeddedJson<T>(string resourceName)
    {
        return LoadEmbeddedResource(resourceName, stream =>
                                                  {
                                                      using var reader = new StreamReader(stream);
                                                      var       json   = reader.ReadToEnd();
                                                      return JsonSerializer.Deserialize<T>(json,
                                                                                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                                  });
    }

    internal static T? ReadLocalJsonFile<T>(string fileName)
    {
        var filePath = $"{Svc.PluginInterface.AssemblyLocation.Directory}\\Data\\{fileName}";
        if (!File.Exists(filePath))
        {
            FullError($"File '{filePath}' not found.");
            return default;
        }

        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        var       json   = reader.ReadToEnd();

        return JsonSerializer.Deserialize<T>(json,
                                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public static string ExtractTextExtended(this ReadOnlySeString s) => s.ExtractText()
                                                                          .Replace("\u00AD", string.Empty)
                                                                          .Replace("\u00A0", string.Empty);

    public static string ToTitleCaseExtended(in ReadOnlySeString s, ClientLanguage language) => string.Intern(s.ExtractTextExtended()
                                                                                                               .ToUpper(true, true, false, language));

    /*public static Regex ToRegex(this ReadOnlySeString seString) => new(string.Join("", seString.Select(payload => payload.Type == ReadOnlySePayloadType.Text
                                                                                                                          ? Regex.Escape(payload.ToString())
                                                                                                                          : "(.*?)\\s*")));*/
    public static Regex ToRegex(this ReadOnlySeString seString)
    {
        var parts = new List<string>();

        foreach (var payload in seString)
        {
            if (payload.Type == ReadOnlySePayloadType.Text)
            {
                var text = payload.ToString()
                                  .Replace('\u00A0', ' ')
                                  .Replace('\u2009', ' ')
                                  .Replace('\u202F', ' ')
                                  .Replace('\t', ' ')
                                  .Replace('\r', ' ')
                                  .Replace('\n', ' ');

                parts.Add(Regex.Escape(text).Replace(" ", @"s+"));
                //parts.Add(Regex.Escape(text));
                continue;
            }

            switch (payload.MacroCode)
            {
                // 1. Formatting (ignored)
                case MacroCode.Color:
                case MacroCode.EdgeColor:
                case MacroCode.ShadowColor:
                case MacroCode.Bold:
                case MacroCode.Italic:
                case MacroCode.Edge:
                case MacroCode.Shadow:
                case MacroCode.ColorType:
                case MacroCode.EdgeColorType:
                case MacroCode.Ruby:
                case MacroCode.Scale:
                case MacroCode.Key:
                case MacroCode.SwitchPlatform:
                case MacroCode.Sound:
                    parts.Add(@"\s*");
                    break;

                // 2. Whitespace / spacing
                case MacroCode.NewLine:
                case MacroCode.NonBreakingSpace:
                case MacroCode.SoftHyphen:
                    parts.Add(@"\s*");
                    break;

                case MacroCode.Hyphen:
                    parts.Add("-");
                    break;

                // 3. Dynamic visible content
                case MacroCode.Icon:
                case MacroCode.Icon2:
                case MacroCode.String:
                case MacroCode.Caps:
                case MacroCode.Head:
                case MacroCode.HeadAll:
                case MacroCode.Lower:
                case MacroCode.LowerHead:
                case MacroCode.Num:
                case MacroCode.Hex:
                case MacroCode.Kilo:
                case MacroCode.Byte:
                case MacroCode.Sec:
                case MacroCode.Time:
                case MacroCode.Float:
                case MacroCode.Digit:
                case MacroCode.Ordinal:
                case MacroCode.PcName:
                case MacroCode.Sheet:
                case MacroCode.SheetSub:
                case MacroCode.JaNoun:
                case MacroCode.EnNoun:
                case MacroCode.DeNoun:
                case MacroCode.FrNoun:
                case MacroCode.ChNoun:
                case MacroCode.LevelPos:
                case MacroCode.Link:
                    parts.Add(@"(.+?)");
                    break;

                // 4. Conditional / structural macros
                case MacroCode.If:
                case MacroCode.Switch:
                case MacroCode.IfPcGender:
                case MacroCode.IfPcName:
                case MacroCode.IfSelf:
                case MacroCode.Josa:
                case MacroCode.Josaro:
                case MacroCode.SetTime:
                case MacroCode.SetResetTime:
                case MacroCode.Wait:
                    parts.Add(@".*?");
                    break;

                // 5. Unknown - Fallback
                default:
                    parts.Add(@".*?");
                    break;
            }
        }

        var pattern = string.Join("", parts);
        return new Regex(pattern, RegexOptions.IgnoreCase);
    }

    // Why? Because fucking french... that's why
    public static string NormalizeWhitespaces(this string s)
    {
        if (s == null)
            return "";

        return s
              .Replace('\u00A0', ' ') // NBSP
              .Replace('\u202F', ' ') // Narrow NBSP
              .Replace('\u2009', ' ') // Thin space
              .Replace('\u200A', ' ') // Hair space
              .Replace('\u2002', ' ') // En space
              .Replace('\u2003', ' ') // Em space
              .Replace('\u2004', ' ') // Three-per-em space
              .Replace('\u2005', ' ') // Four-per-em space
              .Replace('\u2006', ' ') // Six-per-em space
              .Replace('\u2007', ' ') // Figure space
              .Replace('\u2008', ' ') // Punctuation space
              .Replace('\u00AD', ' ') // Soft hyphen
              .Replace('\t', ' ')
              .Replace('\r', ' ')
              .Replace('\n', ' ')
              .Trim();
    }



    public static void DrawIcon(uint iconId, float scale = 1)
    {
        var iconSize = new Vector2(40, 40) * ImGuiHelpers.GlobalScale * scale;
        var texture = Svc.Texture.GetFromGameIcon(iconId)
                         .GetWrapOrEmpty();
        ImGui.Image(texture.Handle, iconSize);
    }

    public static string ToJson<TEnum>(this       TEnum  value) where TEnum : struct, Enum                                       => JsonSerializer.Serialize(value, EnumAsStringOptions);
    public static TEnum  FromJsonEnum<TEnum>(this string json, JsonSerializerOptions? options = null) where TEnum : struct, Enum => JsonSerializer.Deserialize<TEnum>(json, options ?? EnumAsStringOptions);

    public static string ToJson<T>(this T value, JsonSerializerOptions? options = null) where T : class => JsonSerializer.Serialize(value, options ?? JsonDefaults.Options);

    public static T FromJson<T>(this string json, JsonSerializerOptions? options = null) where T : class => JsonSerializer.Deserialize<T>(json, options ?? JsonDefaults.Options)!;

    public static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Options = new()
                                                               {
                                                                       PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                       WriteIndented        = false,
                                                                       NumberHandling       = JsonNumberHandling.AllowReadingFromString
                                                               };
    }

    public static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return cleaned;
    }


    public static TConfig? GetFeatureConfig<TFeature, TConfig>()
            where TFeature : FeatureUI<TConfig>
            where TConfig : IConfig
    {
        if (TryGetFeature<TFeature>(out var feature))
            return feature.Configuration;

        FullError($"{typeof(TFeature).Name} not loaded. Can't get config!");
        return default;
    }
}
