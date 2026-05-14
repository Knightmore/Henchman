namespace Henchman.Extensions;

public static class DictionaryExtensions
{
    public static void ForEach<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            Action<TKey, TValue>           action)
    {
        foreach (var kv in dict)
            action(kv.Key, kv.Value);
    }
}
