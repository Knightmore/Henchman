using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Features.OnYourMark;
using Underlings.Modules;

namespace Henchman.Tweaks;

[Module]
[Confirmation(publicOnly: true)]
public partial class HacksUI : ModuleUI
{
    public override string          Name     => "Hacks";
    public override Enum            Category => Henchman.Category.Tweaks;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Radiation;

    public override Action? Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    [UiCheckbox(typeof(HacksUI), "On Your Mark", "Accept hunt bills remotely", "This will skip all teleporting and walking to each hunt board.\nAll enabled Hunt bills will be accepted from anywhere you are.", BuildRestriction.Public)]
    public static bool AcceptanceHack
    {
        get => OnYourMark.AcceptanceHack;
        set => OnYourMark.AcceptanceHack = value;
    }

    [SigHook("E8 ?? ?? ?? ?? 48 ?? ?? ?? ?? 48 ?? ?? C6 05 ?? ?? ?? ?? ?? 48 ?? ?? ??", "Movement", "No Fall Damage", restriction: BuildRestriction.Public)]
    private static int NoFallDamage(long actor, uint flags) => 0;

    [MemberFunctionHook(typeof(PlayerState), nameof(PlayerState.MemberFunctionPointers.GetGrandCompanyRank), "Grand Company", "Enforce Expert Delivery", "Forces the expert delivery window to show regardless of rank.\nOnly in effect if you do not have expert delivery unlocked.\n(Restored from CBT)", BuildRestriction.Public, true)]
    public static unsafe byte GetGrandCompanyRank(PlayerState* thisPtr)
    {
        var ret = GetGrandCompanyRankHook!.Original(thisPtr);
        return ret < 6
                       ? (byte)6
                       : ret;
    }
}
