using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Henchman.Windows.Layout;

namespace Henchman.Windows;

public class StatusWindow : Window, IDisposable
{
    private const float ExpandedWidth   = 350f;
    private const float ExpandedHeight  = 160f;
    private const float CollapsedHeight = 40f;

    public StatusWindow()
            : base("##HenchmanStatusWindow",
                   ImGuiWindowFlags.NoTitleBar        |
                   ImGuiWindowFlags.NoScrollbar       |
                   ImGuiWindowFlags.NoResize          |
                   ImGuiWindowFlags.NoScrollWithMouse |
                   ImGuiWindowFlags.NoBackground      |
                   ImGuiWindowFlags.NoCollapse)
    {
        SizeConstraints = new WindowSizeConstraints
                          {
                                  MinimumSize = new Vector2(ExpandedWidth, ExpandedHeight),
                                  MaximumSize = new Vector2(ExpandedWidth, ExpandedHeight)
                          };

        Size          = new Vector2(ExpandedWidth, ExpandedHeight);
        SizeCondition = ImGuiCond.FirstUseEver;

        RespectCloseHotkey = false;
        IsOpen             = false;
    }

    public bool   IsCollapsed { get; set; }
    public string StatusText  { get; set; } = string.Empty;
    public string DetailsText { get; set; } = string.Empty;
    public float  Progress    { get; set; }

    public Action? OnPause { get; set; }
    public Action? OnAbort { get; set; }

    public void Dispose() { }

    public override void Draw()
    {
        using (Theme.Push())
        {
            var height = IsCollapsed
                                 ? CollapsedHeight
                                 : ExpandedHeight;
            ImGui.SetNextWindowSize(new Vector2(ExpandedWidth, height), ImGuiCond.Always);

            using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.14f, 0.14f, 0.14f, 0.95f)))
            {
                using (var child = ImRaii.Child("##StatusWindowContent", Vector2.Zero, true))
                {
                    if (child.Success)
                    {
                        DrawHeader();
                        DrawContent();
                    }
                }
            }
        }
    }


    private void DrawHeader()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary))
            ImGui.Text("HENCHMAN STATUS");

        ImGui.SameLine(ImGui.GetContentRegionAvail()
                            .X -
                       20);

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.SmallButton(FontAwesomeIcon.Times.ToIconString()))
            IsOpen = false;
        ImGui.PopFont();
    }

    private void DrawContent()
    {
        ImGui.Spacing();

        if (!string.IsNullOrEmpty(TaskName))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.SuccessGreen))
                ImGui.Text($"✓ {TaskName}");
        }

        if (TaskDescription.Count > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.AccentPink))
                ImGui.Text($"Current Task: {TaskDescription.Last()}");
        }

        if (!string.IsNullOrEmpty(DetailsText))
        {
            ImGui.Spacing();
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary))
                ImGui.TextWrapped(DetailsText);
        }

        ImGui.Spacing();
        DrawProgressBar();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawActionButtons();
    }

    private void DrawProgressBar()
    {
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, Theme.AccentPink))
            ImGui.ProgressBar(Progress, new Vector2(-1, 6), string.Empty);
    }

    private void DrawActionButtons()
    {
        var buttonWidth = (ImGui.GetContentRegionAvail()
                                .X -
                           ImGui.GetStyle()
                                .ItemSpacing.X) /
                          2;

        if (OnPause != null)
        {
            if (ImGui.Button("Pause", new Vector2(buttonWidth, 0)))
                OnPause();

            ImGui.SameLine();
        }

        if (Running && CurrentTaskRecord != null)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonDanger))
            {
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Theme.ButtonDangerHovered))
                {
                    var abortButtonWidth = OnPause != null
                                                   ? buttonWidth
                                                   : -1;

                    if (ImGui.Button("Abort", new Vector2(abortButtonWidth, 0))) CancelAllTasks();
                }
            }
        }
    }
}
