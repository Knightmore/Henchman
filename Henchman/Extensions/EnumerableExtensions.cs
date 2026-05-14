using System.Threading.Tasks;

namespace Henchman.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> If<T>(
            this IEnumerable<T>                  source,
            bool                                 condition,
            Func<IEnumerable<T>, IEnumerable<T>> transform) => condition
                                                                       ? transform(source)
                                                                       : source;

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);
    }

    public static async Task ForEachAsync<T>(
            this IEnumerable<T> source,
            Func<T, Task>       action)
    {
        foreach (var item in source) await action(item);
    }
}
