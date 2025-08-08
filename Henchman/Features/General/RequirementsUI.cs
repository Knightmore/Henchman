using Henchman.Helpers;

namespace Henchman.Features.General;

internal class RequirementsUI : FeatureUI
{
    public override string  Name        => "Requirements";
    public override Action? Help        { get; }
    public override bool    LoginNeeded => false;

    public override void Draw()
    {
        ImGuiHelper.DrawRequirements(FeatureSet);
    }
}
