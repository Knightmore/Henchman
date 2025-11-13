using System.Collections;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Generated;
using Henchman.Helpers;

namespace Henchman.Multibox.Command;

public static class CommandProcessor
{
    public static async Task<(object? returnValue, CommandEnvelope env)> HandleRPCAsync(string json, CancellationToken token = default)
    {
        var env = JsonSerializer.Deserialize<CommandEnvelope>(json, Utils.JsonDefaults.Options) ?? throw new ArgumentException("Invalid message.");

        if (!Enum.TryParse<CommandKey>(env.Key, out var key)) throw new InvalidOperationException("Invalid Command Key.");
        Verbose($"RPC: {env.Key}");
        if (!CommandRegistry.TryGetMeta(key, out var meta))
            throw new InvalidOperationException($"Unknown command: {env.Key}");

        foreach (var jsonElement in env.Args) Verbose($"{jsonElement.GetRawText()} {jsonElement.ValueKind}");
        var typedArgs = MaterializeArgs(env.Args, meta);
        return (await CommandDispatcher.DispatchAsync(key, typedArgs, token), env);
    }

    private static object[] MaterializeArgs(JsonElement[] rawArgs, CommandMeta meta)
    {
        var expected = meta.ParameterTypes;
        var filtered = expected.Where(t => t != typeof(CancellationToken))
                               .ToArray();

        if (rawArgs.Length != filtered.Length)
            throw new ArgumentException($"Incorrect argument count for command {meta}. Got: {rawArgs.Length} - Expected: {filtered.Length}");

        var result   = new object[expected.Length];
        var rawIndex = 0;

        for (var i = 0; i < expected.Length; i++)
        {
            if (expected[i] == typeof(CancellationToken))
            {
                result[i] = null!;
                continue;
            }

            result[i] = Translate(rawArgs[rawIndex], expected[i]);
            rawIndex++;
        }

        return result;
    }

    private static object Translate(JsonElement el, Type targetType)
    {
        if (el.ValueKind == JsonValueKind.Null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
                throw new ArgumentNullException($"Argument cannot be null for {targetType.Name}.");
            return null!;
        }

        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        Verbose($"Translating {el.GetRawText()} with type {actualType}");

        if (actualType.IsEnum)
        {
            return el.ValueKind == JsonValueKind.String
                           ? Enum.Parse(actualType, el.GetString()!, true)
                           : Enum.ToObject(actualType, el.GetInt32());
        }

        if (actualType == typeof(string)) return el.GetString()!;
        if (actualType == typeof(bool)) return el.GetBoolean();
        if (actualType == typeof(byte)) return el.GetByte();
        if (actualType == typeof(sbyte)) return el.GetSByte();
        if (actualType == typeof(short)) return el.GetInt16();
        if (actualType == typeof(ushort)) return el.GetUInt16();
        if (actualType == typeof(int)) return el.GetInt32();
        if (actualType == typeof(uint)) return el.GetUInt32();
        if (actualType == typeof(long)) return el.GetInt64();
        if (actualType == typeof(ulong)) return el.GetUInt64();
        if (actualType == typeof(float))
        {
            return el.ValueKind == JsonValueKind.Number
                           ? el.GetSingle()
                           : float.Parse(el.GetString()!);
        }

        if (actualType == typeof(double)) return el.GetDouble();
        if (actualType == typeof(decimal)) return el.GetDecimal();

        if (actualType == typeof(Vector3))
            return ReadVector3(el);

        if (actualType.IsArray)
        {
            var elemType = actualType.GetElementType()!;
            var jsonElems = el.EnumerateArray()
                              .ToArray();
            var array = Array.CreateInstance(elemType, jsonElems.Length);
            for (var i = 0; i < jsonElems.Length; i++) array.SetValue(Translate(jsonElems[i], elemType), i);
            return array;
        }

        if (actualType.IsGenericType)
        {
            var genericDef = actualType.GetGenericTypeDefinition();
            var genArgs    = actualType.GetGenericArguments();

            if (genericDef == typeof(List<>))
            {
                var elemType = genArgs[0];
                var listType = typeof(List<>).MakeGenericType(elemType);
                var list     = (IList)Activator.CreateInstance(listType)!;
                foreach (var je in el.EnumerateArray())
                    list.Add(Translate(je, elemType));
                return list;
            }

            if (genericDef == typeof(Dictionary<,>) && genArgs[0] == typeof(string))
            {
                var valueType = genArgs[1];
                var dictType  = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                var dict      = (IDictionary)Activator.CreateInstance(dictType)!;

                foreach (var prop in el.EnumerateObject())
                    dict[prop.Name] = Translate(prop.Value, valueType);

                return dict;
            }
        }

        return JsonSerializer.Deserialize(el.GetRawText(), actualType, Utils.JsonDefaults.Options) ?? throw new InvalidOperationException($"Failed to deserialize type {targetType.FullName}.");
    }

    private static Vector3 ReadVector3(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                var   e = el.EnumerateArray();
                float x = NextFloat(ref e), y = NextFloat(ref e), z = NextFloat(ref e);
                return new Vector3(x, y, z);

            case JsonValueKind.Object:
                return new Vector3(
                                   ReadFloat(el, "X", "x"),
                                   ReadFloat(el, "Y", "y"),
                                   ReadFloat(el, "Z", "z")
                                  );

            default:
                throw new ArgumentException("Vector3 must be an array [x,y,z] or object {X,Y,Z}.");
        }

        static float NextFloat(ref JsonElement.ArrayEnumerator en)
        {
            if (!en.MoveNext()) throw new ArgumentException("Vector3 array must have 3 elements.");
            var cur = en.Current;
            return cur.ValueKind == JsonValueKind.Number
                           ? cur.GetSingle()
                           : float.Parse(cur.GetString()!);
        }

        static float ReadFloat(JsonElement obj, params string[] names)
        {
            foreach (var n in names)
            {
                if (obj.TryGetProperty(n, out var prop))
                {
                    return prop.ValueKind == JsonValueKind.Number
                                   ? prop.GetSingle()
                                   : float.Parse(prop.GetString()!);
                }
            }

            throw new ArgumentException($"Missing Vector3 component ({string.Join("/", names)}).");
        }
    }
}
