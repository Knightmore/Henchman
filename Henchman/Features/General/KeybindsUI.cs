using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Underlings.Modules;
using Underlings.Ui;
using Action = System.Action;

namespace Henchman.Features.General;

[Module]
internal class KeybindsUI : ModuleUI
{
    public override string          Name     => "Keybinds";
    public override Enum            Category => Henchman.Category.System;
    public override FontAwesomeIcon Icon     => FontAwesomeIcon.Keyboard;

    public override Action Help => () => { ImGui.Text(T("HelpText")); };

    public override bool LoginNeeded => false;

    public override void Draw() => KeybindsUi.Draw();
}
