using Dalamud.Interface.Windowing;
using Henchman.Helpers;

namespace Henchman.Features.General;

[General]
internal class RequirementsUI : FeatureUI
{
    public override string  Name        => "Requirements";
    public override Action? Help        { get; }
    public override bool    LoginNeeded => false;
    public override Window.WindowSizeConstraints SizeConstraints { get; } = new Window.WindowSizeConstraints
                                                                            {
                                                                                    MinimumSize = new Vector2(400, 400),
                                                                                    MaximumSize = new Vector2(400, 400)
                                                                            };
    public override void Draw()
    {
        ImGuiHelper.DrawRequirements(FeatureSet);
    }
}
