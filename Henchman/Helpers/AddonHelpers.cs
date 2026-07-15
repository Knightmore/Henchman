using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Henchman.Helpers;

internal static class AddonHelpers
{
    internal static unsafe bool SelectPreset(byte presetPosition)
    {
        if (TryGetAddonByName<AtkUnitBase>("CharaMakeDataImport", out var charaMakeDataImportAddon) && charaMakeDataImportAddon->IsFullyLoaded())
        {
            charaMakeDataImportAddon->FireCallback(true, 102, (int)presetPosition, false);
            return true;
        }

        return false;
    }
}
