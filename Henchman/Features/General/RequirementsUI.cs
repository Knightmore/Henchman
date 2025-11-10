using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Henchman.Helpers;

namespace Henchman.Features.General;

[Feature]
internal class RequirementsUI : FeatureUI
{
    public override string          Name        => "Requirements";
    public override string          Category    => Henchman.Category.System;
    public override FontAwesomeIcon Icon        => FontAwesomeIcon.ExclamationTriangle;
    public override Action?         Help        { get; }
    public override bool            LoginNeeded => false;

    public override void Draw()
    {
        ImGuiHelper.DrawRequirements(FeatureSet);
    }
}
