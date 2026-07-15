namespace Henchman.Tweaks;

internal static class SkipTalk
{
    private static readonly AddonAutoClickGate Gate = new("Talk");

    internal static void Tick() => Gate.Tick();

    internal static void SetTemporary() => Gate.SetTemporary();

    internal static void UnsetTemporary() => Gate.UnsetTemporary();
}
