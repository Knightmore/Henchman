using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Underlings.Modules;
using static Henchman.Tweaks.Rendering;

namespace Henchman.Tweaks;

[Module]
public partial class RenderingUI : ModuleUI
{
    public override string          Name     => "Rendering";
    public override Enum            Category => Henchman.Category.Tweaks;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Box;

    public override Action? Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    [UiCheckbox(typeof(RenderingUI), "Performance", "Disable Render", "This will disable your 3D rendering to minimize your GPU load but keeps the whole UI intact.\nTo get the best result, add a framerate limit of 30 FPS to it.\nThere may be other plugins which are forcing this state, so if this is set to something you did not expect, it's not my fault.\nIf you don't want other plugins to force their state on your, use the checkbox below!\n\nTHIS IS NON-PERSISTENT AND WILL RESET ON EXITING THE GAME OR DISABLING THE PLUGIN!", BuildRestriction.Public)]
    public static bool DisableRender
    {
        get => RenderDisabled;
        set => SetRender(!value);
    }

    [UiCheckbox(typeof(RenderingUI), "Performance", "Disable Render When Unfocused", "Automatically disables 3D rendering whenever the game window loses focus, and re-enables it once it regains focus. You don't need to toggle Disable Render yourself while this is on.", BuildRestriction.Public, persist: true, parent: nameof(DisableRender))]
    public static bool DisableRenderWhenUnfocused
    {
        get => Rendering.DisableRenderWhenUnfocused;
        set => SetDisableRenderWhenUnfocused(value);
    }

    [UiCheckbox(typeof(RenderingUI), "Performance", "Force Renderstate", "Enable this if you don't give a shit about other plugins trying to set the render mode and you want Henchman to be the single point of responsiblity!\n\nTHIS IS NON-PERSISTENT AND WILL RESET ON EXITING THE GAME OR DISABLING THE PLUGIN!", BuildRestriction.Public, parent: nameof(DisableRender))]
    public static bool ForceRender
    {
        get => ForceRenderEnabled;
        set => SetForceRenderEnabled(value);
    }

    [SigHook("48 83 EC 28 80 B9 ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 80 B9 ?? ?? ?? ?? ??", "Fade", "Skip Fade", "Skips all fade transitions, such as when changing zones.\n\nThis could mess with other plugins which rely on checking fading.", BuildRestriction.Public, true)]
    private static unsafe void AddonFadeMiddleBack_Draw(AtkUnitBase* addon) { }

    public override void Dispose()
    {
        DisposeSigHooks();
    }
}
