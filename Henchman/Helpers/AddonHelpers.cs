using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECommons.Automation;

namespace Henchman.Helpers
{
    internal static class AddonHelpers
    {
        internal static unsafe bool SelectPreset(byte presetPosition)
        {
            if (TryGetAddonByName<AtkUnitBase>("CharaMakeDataImport", out var charaMakeDataImportAddon) && charaMakeDataImportAddon->IsFullyLoaded())
            {
                Callback.Fire(charaMakeDataImportAddon, true, 102, (int)presetPosition, false);
                return true;
            }

            return false;
        }
    }
}
