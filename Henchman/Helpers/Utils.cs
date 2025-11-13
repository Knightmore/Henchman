using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons.GameHelpers;
using Lumina.Text.ReadOnly;

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

    public static Regex ToRegex(this ReadOnlySeString seString) => new(string.Join("", seString.Select(payload => payload.Type == ReadOnlySePayloadType.Text
                                                                                                                          ? Regex.Escape(payload.ToString())
                                                                                                                          : "(.*)")));

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
}
