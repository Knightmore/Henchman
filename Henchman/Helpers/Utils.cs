using Dalamud.Game;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Henchman.Helpers;

internal static class Utils
{
    internal static bool IsPluginBusy => Running;

    internal static unsafe byte? GetGearsetForClassJob(ClassJob cj)
    {
        byte? backup = null;
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++)
        {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->Id != i) continue;
            if (gearset->ClassJob == cj.RowId) return gearset->Id;
            if (backup == null && cj.ClassJobParent.RowId != 0 && gearset->ClassJob == cj.ClassJobParent.RowId) backup = gearset->Id;
        }

        return backup;
    }

    internal static List<Vector3> SortListByDistance(List<Vector3> pointList)
    {
        double DistanceSquaredTo(Vector3 main, Vector3 other)
        {
            double dx = main.X - other.X;
            double dy = main.Y - other.Y;
            double dz = main.Z - other.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        var path = new List<Vector3> { Player.Position };
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
                                                      var json = reader.ReadToEnd();
                                                      return JsonSerializer.Deserialize<T>(json,
                                                                                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                                  });
    }

    internal static T? ReadLocalJsonFile<T>(string fileName)
    {
        var filePath = $"{Svc.PluginInterface.AssemblyLocation.Directory}\\Data\\{fileName}";
        Verbose(filePath);
        if (!File.Exists(filePath))
            Error($"File '{filePath}' not found.");

        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<T>(json,
                                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }


    internal static async Task<T?> ReadRemoteJsonAsync<T>(string fileName)
    {
        var url = $"https://raw.githubusercontent.com/Knightmore/Henchman/refs/heads/main/Henchman/Data/{fileName}";
        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json,
                                                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Error($"Error reading JSON from '{url}': {ex.Message}");
            return default;
        }
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
        ImGui.Image(texture.ImGuiHandle, iconSize);
    }
}
