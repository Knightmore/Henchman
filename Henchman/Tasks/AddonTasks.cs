using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Features.RetainerVocate;
using Henchman.Helpers;
using Lumina.Text.ReadOnly;
using System.Threading.Tasks;

namespace Henchman.Tasks;

internal class AddonTasks
{
    internal static async Task<bool> ClickAddonButton(string addonName, uint buttonNodeId)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
            {
                var button = addon->GetButtonNodeById(buttonNodeId);
                if (button->IsEnabled && button->AtkResNode->IsVisible())
                {
                    button->ClickAddonButton(addon);
                    return true;
                }
            }
        }

        await Task.Delay(100);
        return false;
    }

    internal static async Task<bool> ProcessYesNo(bool accept, string compare)
    {
        if (TryGetAddonMaster<AddonMaster.SelectYesno>(out var addon) && addon.IsAddonReady)
        {
            if (addon.SeStringNullTerminated.ToString()
                     .Contains(compare))
            {
                if (accept)
                    addon.Yes();
                else
                    addon.No();

                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }

    /*internal static async Task<bool> GenericYesNo(bool accept)
    {
        if (TryGetAddonMaster<AddonMaster.SelectYesno>(out var addon) && addon.IsAddonReady)
        {
            if (accept)
                addon.Yes();
            else
                addon.No();

            return true;
        }

        await Task.Delay(100);
        return false;
    }*/

    internal static async Task<bool> RegexYesNo(bool accept, ReadOnlySeString text)
    {
        if (TryGetAddonMaster<AddonMaster.SelectYesno>(out var addon) && addon.IsAddonReady)
        {
            if (text.ToRegex()
                    .IsMatch(addon.SeStringNullTerminated.TextValue))
            {
                if (accept)
                    addon.Yes();
                else
                    addon.No();

                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }

    internal static Task<bool> TrySelectSpecificEntry(string text)
    {
        return TrySelectSpecificEntry(x => x.StartsWith(text, StringComparison.InvariantCultureIgnoreCase));
    }

    internal static Task<bool> TrySelectSpecificEntry(IEnumerable<string> text)
    {
        return TrySelectSpecificEntry(x => x.StartsWithAny(text, StringComparison.InvariantCultureIgnoreCase));
    }

    internal static async Task<bool> TrySelectSpecificEntry(Func<string, bool> inputTextTest)
    {
        unsafe
        {
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                if (new AddonMaster.SelectString(addon).Entries.TryGetFirst(x => inputTextTest(x.Text), out var entry))
                {
                    entry.Select();
                    Log($"TrySelectSpecificEntry: selecting {entry}");
                    return true;
                }
            }
        }

        await Task.Delay(100);
        return false;
    }

    internal static async Task<bool> TrySelectFirstExplorationVenture(uint retainerClassId)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerTaskList", out var addon) && IsAddonReady(addon))
            {
                if (RetainerVocate.IsCombat(retainerClassId))
                {
                    Callback.Fire(addon, true, 11, 343);
                    return true;
                }

                switch (retainerClassId)
                {
                    // Miner
                    case 16:
                        Callback.Fire(addon, true, 11, 356);
                        break;
                    // Botanist
                    case 17:
                        Callback.Fire(addon, true, 11, 369);
                        break;
                    // Fisher
                    case 18:
                        Callback.Fire(addon, true, 11, 382);
                        break;
                }

                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }

    internal static async Task<bool> TryClickRetainerTaskAskAssign()
    {
        unsafe
        {
            if (TryGetAddonByName<AddonRetainerTaskAsk>("RetainerTaskAsk", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                new AddonMaster.RetainerTaskAsk(addon).Assign();
                return true;
            }
        }

        await Task.Delay(100);
        return false;
    }
}
