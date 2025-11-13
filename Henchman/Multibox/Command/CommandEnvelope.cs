using System.Globalization;
using System.Text.Json;
using Henchman.Helpers;

namespace Henchman.Multibox.Command;

public sealed class CommandEnvelope
{
    public string        Key  { get; init; } = "";
    public JsonElement[] Args { get; init; } = [];

    public static string Create(string key, object?[]? args = null, JsonSerializerOptions? options = null)
    {
        args    ??= [];
        options ??= Utils.JsonDefaults.Options;

        var elems = new JsonElement[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is null)
            {
                elems[i] = JsonDocument.Parse("null")
                                       .RootElement.Clone();
                continue;
            }

            if (args[i] is Vector3 v)
            {
                var json = $"[{v.X.ToString("R", CultureInfo.InvariantCulture)},{v.Y.ToString("R", CultureInfo.InvariantCulture)},{v.Z.ToString("R", CultureInfo.InvariantCulture)}]";
                elems[i] = JsonDocument.Parse(json)
                                       .RootElement.Clone();
                continue;
            }

            elems[i] = JsonSerializer.SerializeToElement(args[i], args[i]!.GetType(), options);
        }

        var envelope = new CommandEnvelope { Key = key, Args = elems };
        return envelope.ToJson();
    }

    public string ToJson(JsonSerializerOptions? options = null) => JsonSerializer.Serialize(this, options ?? Utils.JsonDefaults.Options);
}
