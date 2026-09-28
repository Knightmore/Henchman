using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Underlings.Modules;
using static Henchman.Tweaks.Rendering;

namespace Henchman.Tweaks;

[Module]
public partial class RenderingUI : ModuleUI
{
    private static readonly SigPatch FogPatch = new("41 8B 87 70 44 00 00 C1 E8 06 A8 01 0F 84 22 03 00 00 48 8B", [0xE9, 0x23, 0x03, 0x00, 0x00, 0x90], 12);
    private static          bool     underwaterFogDisabled;
    private static          bool     allSceneFogDisabled;

    static RenderingUI()
    {
        RestorePersistedStatic();
        Svc.Condition.ConditionChange += OnConditionChange;
    }

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

    [UiCheckbox(typeof(RenderingUI), "Post-Processing", "Disable Underwater Fog", "Skips scene fog while Diving (#81) is active, even if Water Graphics State is disabled. Normal fog remains active above water.", BuildRestriction.Public, persist: true)]
    public static bool DisableUnderwaterFog
    {
        get => underwaterFogDisabled;
        set
        {
            underwaterFogDisabled = value;
            UpdateFogPatch(Svc.Condition[ConditionFlag.Diving]);
        }
    }

    [UiCheckbox(typeof(RenderingUI), "Post-Processing", "Disable All Scene Fog", "Skips the standard and underwater fog passes everywhere, including above water.", BuildRestriction.Public, persist: true)]
    public static bool DisableAllSceneFog
    {
        get => allSceneFogDisabled;
        set
        {
            allSceneFogDisabled = value;
            UpdateFogPatch(Svc.Condition[ConditionFlag.Diving]);
        }
    }

    private static void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.Diving)
            UpdateFogPatch(value);
    }

    private static void UpdateFogPatch(bool diving)
    {
        if (allSceneFogDisabled || (underwaterFogDisabled && diving))
            FogPatch.Enable();
        else
            FogPatch.Dispose();
    }

    [MemoryPatch("45 84 C0 74 15 E8 ?? ?? ?? ?? 84 C0 75 30 B2 01", "EB 15", "Post-Processing", "Disable Water VFX", "Removes the moving underwater overlay.", BuildRestriction.Public, offset: 3, persist: true)]
    private static void DisableWaterVfx() { }

    [MemoryPatch("44 38 A2 90 0B 00 00 0F 84 E4 01 00 00", "E9 E5 01 00 00 90", "Post-Processing", "Disable Water Fog Gradient", "Uses the ordinary fog path instead of the underwater fog pass.\n\nFog is not getting darker with more depth.", BuildRestriction.Public, offset: 7, persist: true)]
    private static void DisableWaterFog() { }

    [MemoryPatch("48 85 C0 74 07 40 88 B8 90 0B 00 00 48 8B 5C 24", "C6 80 90 0B 00 00 00", "Post-Processing", "Disable Water Graphics State", "Forces the underwater graphics flag off, including underwater lighting and caustics.", BuildRestriction.Public, offset: 5, persist: true)]
    private static void DisableWaterGraphicsState() { }

    [SigHook("48 83 EC 28 80 B9 ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 80 B9 ?? ?? ?? ?? ??", "Fade", "Skip Fade", "Skips all fade transitions, such as when changing zones.\n\nThis could mess with other plugins which rely on checking fading.", BuildRestriction.Public, true)]
    private static unsafe void AddonFadeMiddleBack_Draw(AtkUnitBase* addon) { }

    public override void Dispose()
    {
        Svc.Condition.ConditionChange -= OnConditionChange;
        FogPatch.Dispose();
        DisposeSigHooks();
    }
}
