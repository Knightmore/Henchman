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

    public override string Description => Loc.G("Requirements.HeaderText");

    public override void Draw()
    {
        var features = ModuleRegistry.Instances.OfType<ModuleUI>()
                                     .Where(f => f.Requirements.Count > 0)
                                     .OrderBy(f => f.Category)
                                     .ThenBy(f => f.Name)
                                     .ToList();
        Widgets.Grid(3, 12f, features.Select<ModuleUI, Action<float>>(f => width => Widgets.RequirementsCard(f, null, width, f.DisplayName))
                                     .ToList());
    }
}
