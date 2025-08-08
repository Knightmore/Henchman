using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Helpers;

namespace Henchman.Tweaks;

internal static unsafe class SkipTalk
{
    private static bool WasChanged;

    private static bool IsBusy => Utils.IsPluginBusy;

    internal static void Tick()
    {
        if (WasChanged)
        {
            if (!IsBusy)
            {
                WasChanged = false;
                Disable();
                PluginLog.Debug("SkipTalk disabled");
            }
        }
        else
        {
            if (IsBusy)
            {
                WasChanged = true;
                Enable();
                PluginLog.Debug("SkipTalk enabled");
            }
        }
    }

    internal static void Enable()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    internal static void Disable()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    private static void Click(AddonEvent type, AddonArgs args)
    {
        if (((AtkUnitBase*)args.Addon.Address)->IsVisible) new AddonMaster.Talk(args.Addon).Click();
    }
}
