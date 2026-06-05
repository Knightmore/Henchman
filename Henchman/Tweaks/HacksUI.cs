using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Abstractions;
using Henchman.Features.OnYourMark;

namespace Henchman.Tweaks;

[Feature]
[Confirmation]
public partial class HacksUI : FeatureUI
{
    public override string Name => "Hacks";
    public override Category Category => Category.Tweaks;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Radiation;

    public override Action? Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    [UiCheckbox(typeof(HacksUI), "On Your Mark", "Accept hunt bills remotely", "This will skip all teleporting and walking to each hunt board.\nAll enabled Hunt bills will be accepted from anywhere you are.", BuildRestriction.Public)]
    public static bool AcceptanceHack
    {
        get => OnYourMark.AcceptanceHack;
        set => OnYourMark.AcceptanceHack = value;
    }

#if PRIVATE
    [SigHook("40 53 48 83 EC ?? 48 8B 1D ?? ?? ?? ?? 48 85 DB 0F 84 ?? ?? ?? ?? 80 3D", "Movement", "Unlock Flying", "This unlocks flying in ALL areas where mounting is allowed.\nEven if you don't have progressed the MSQ or collected all Aether Currents for that zone")]
    private static unsafe Control.FlightAllowedStatus GetFlightAllowedStatus()
    {
        if (!Control.Instance()->LocalPlayer->IsMounted())
            return Control.FlightAllowedStatus.NotMounted;

        return Control.FlightAllowedStatus.CanFly;
    }
#endif

    [SigHook("E8 ?? ?? ?? ?? 48 ?? ?? ?? ?? 48 ?? ?? C6 05 ?? ?? ?? ?? ?? 48 ?? ?? ??", "Movement", "No Fall Damage", restriction: BuildRestriction.Public)]
    private static int NoFallDamage(long actor, uint flags) => 0;

    [MemberFunctionHook(typeof(PlayerState), nameof(PlayerState.MemberFunctionPointers.GetGrandCompanyRank), "Grand Company", "Enforce Expert Delivery", "Forces the expert delivery window to show regardless of rank.\nOnly in effect if you do not have expert delivery unlocked.\n(Restored from CBT)", BuildRestriction.Public)]
    public static unsafe byte GetGrandCompanyRank(PlayerState* thisPtr)
    {
        var ret = GetGrandCompanyRankHook.Original(thisPtr);
        return ret < 6
                       ? (byte)17
                       : ret;
    }
}
