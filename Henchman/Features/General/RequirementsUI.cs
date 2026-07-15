using System.Linq;
using Dalamud.Interface;
using Underlings.Modules;

namespace Henchman.Features.General;

[Module]
internal class RequirementsUI : ModuleUI
{
    public override string          Name        => "Requirements";
    public override Enum            Category    => Henchman.Category.System;
    public override FontAwesomeIcon Icon        => FontAwesomeIcon.ExclamationTriangle;
    public override Action?         Help        { get; }
    public override bool            LoginNeeded => false;

    public override void Draw()
    {
        DrawRequirements(ModuleRegistry.Instances.OfType<ModuleUI>());
    }
}
