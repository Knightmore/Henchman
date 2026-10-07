namespace Henchman.Features.OnYourMark;

internal static class HuntExpansion
{
    // These are persisted configuration keys, not localized display names.
    internal static string? GetKey(uint rowId) => rowId switch
    {
        0 => "A Realm Reborn",
        1 => "Heavensward",
        2 => "Stormblood",
        3 => "Shadowbringers",
        4 => "Endwalker",
        5 => "Dawntrail",
        _ => null
    };
}
