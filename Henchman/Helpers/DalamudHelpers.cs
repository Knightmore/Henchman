using System.Reflection;
using System.Runtime.InteropServices;

namespace Henchman.Helpers;

internal static class DalamudHelpers
{
    public static nint GetMemberFuncByName(Type staticType, string propertyName) => (nint)(staticType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
                                                                                                    ?.GetValue(null) ??
                                                                                           throw new MissingMemberException(staticType.FullName, propertyName));

    public static int GetFieldOffset<T>(string fieldName)
    {
        var field = typeof(T).GetField(
                                       fieldName,
                                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null)
            throw new MissingFieldException(typeof(T).FullName, fieldName);

        return field.GetCustomAttribute<FieldOffsetAttribute>()
                   ?.Value ??
               throw new InvalidOperationException($"{fieldName} has no FieldOffsetAttribute");
    }
}
