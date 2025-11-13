using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace Henchman.Windows.Layout;

public record NavCategory(FontAwesomeIcon Icon, List<NavItem> Items, bool Collapsed = true)
{
    public bool Collapsed { get; set; } = Collapsed;
}

public record NavItem(string Name, FontAwesomeIcon Icon, Action OnClick);

public class Sidebar(ImTextureID logoTextureHandle = default)
{
    private readonly List<KeyValuePair<string, NavCategory>> categories = [];
    public           string?                                 ActiveItemName;

    public static float GlobalFontScale => ImGui.GetIO()
                                                .FontGlobalScale;

    private float expandedWidth  => 200f * GlobalFontScale;
    private float collapsedWidth => 50f  * GlobalFontScale;
    public  bool  IsCollapsed    { get; set; }

    public KeyValuePair<string, NavCategory> AddCategory(string name, FontAwesomeIcon icon)
    {
        var existing = categories.FirstOrDefault(pair => pair.Key == name);

        if (existing.Value != null) return existing;

        var newCategory = new KeyValuePair<string, NavCategory>(name, new NavCategory(icon, new List<NavItem>()));
        categories.Add(newCategory);
        return newCategory;
    }


    public void AddFeature(string categoryName, NavItem navItem)
    {
        var category = categories.FirstOrDefault(pair => pair.Key == categoryName);

        if (!category.Equals(default(KeyValuePair<string, NavCategory>))) category.Value.Items.Add(navItem);
    }

    public Vector2 Draw(Action onLogoClick)
    {
        var width = IsCollapsed
                            ? collapsedWidth
                            : expandedWidth;

        using (var child = ImRaii.Child("##Sidebar", new Vector2(width, 0), true))
        {
            if (!child.Success) return Vector2.Zero;

            DrawLogo(onLogoClick);

            /*if (!IsCollapsed)
                DrawSearchBox();*/

            DrawCategories();

            var oldCursorPos = ImGui.GetCursorPos();
            var buttonSize   = new Vector2(24 * GlobalFontScale, 40 * GlobalFontScale);
            var windowHeight = ImGui.GetWindowHeight();
            var buttonY      = (windowHeight / 2) - (buttonSize.Y / 2);
            var posX = oldCursorPos.X +
                       ImGui.GetContentRegionAvail()
                            .X +
                       (buttonSize.X / (4 * GlobalFontScale));
            return new Vector2(posX, buttonY);
        }
    }

    internal void DrawCollapseButton()
    {
        var buttonSize = new Vector2(24, 40);

        var icon = IsCollapsed
                           ? FontAwesomeIcon.ChevronRight
                           : FontAwesomeIcon.ChevronLeft;

        if (ImGuiComponents.IconButton(
                                       icon,
                                       Theme.BackgroundCard,
                                       Theme.Border,
                                       Theme.AccentPink,
                                       buttonSize))
            IsCollapsed = !IsCollapsed;
        ImGui.SetItemAllowOverlap();
    }

    private void DrawLogo(Action onLogoClick)
    {
        var logoSize = IsCollapsed
                               ? 30f * GlobalFontScale
                               : 80f * GlobalFontScale;
        var centerX = (IsCollapsed
                               ? collapsedWidth
                               : expandedWidth) /
                      2;

        ImGui.SetCursorPosX(centerX - (logoSize / 2));

        if (!logoTextureHandle.Equals(default))
        {
            ImGui.Image(logoTextureHandle, new Vector2(logoSize, logoSize));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) onLogoClick();
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(1f, 0.42f, 0.62f, 0.1f)))
            {
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.42f, 0.62f, 0.15f)))
                {
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(1f, 0.42f, 0.62f, 0.2f)))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Border, Theme.AccentPink))
                        {
                            var style     = ImGui.GetStyle();
                            var oldBorder = style.FrameBorderSize;
                            style.FrameBorderSize = 2f;

                            if (ImGui.Button("H", new Vector2(logoSize, logoSize)))
                                ActiveItemName = string.Empty;

                            style.FrameBorderSize = oldBorder;
                        }
                    }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawSearchBox()
    {
        ImGui.SetNextItemWidth(-1);

        var searchText = string.Empty;
        ImGui.InputTextWithHint("##Search", "Search features...", ref searchText, 256);

        ImGui.Spacing();
    }

    private void DrawCategories()
    {
        foreach (var category in categories) DrawCategory(category.Key, category.Value);
    }

    private void DrawCategory(string categoryName, NavCategory category)
    {
        if (IsCollapsed)
            DrawCollapsedCategory(category);
        else
            DrawExpandedCategory(categoryName, category);
    }

    private void DrawCollapsedCategory(NavCategory category)
    {
        var iconText = category.Icon.ToIconString();

        ImGui.PushFont(UiBuilder.IconFont);
        var textSize = ImGui.CalcTextSize(iconText);
        ImGui.PopFont();

        ImGui.SetCursorPosX((collapsedWidth - textSize.X) / 2);

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary))
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(iconText);
            ImGui.PopFont();
        }

        ImGui.Spacing();

        foreach (var item in category.Items) DrawCollapsedNavItem(item);

        ImGui.Spacing();
    }

    private void DrawExpandedCategory(string categoryName, NavCategory category)
    {
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Theme.BackgroundCard))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextSecondary))
            {
                using (var headerChild = ImRaii.Child($"##CategoryHeader_{categoryName}", new Vector2(0, 30 * GlobalFontScale), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (!headerChild.Success) return;

                    var headerHeight    = 30f * GlobalFontScale;
                    var textHeight      = ImGui.GetTextLineHeight();
                    var verticalPadding = (headerHeight - textHeight) / 2f;

                    ImGui.SetCursorPosY(verticalPadding);

                    var iconText = category.Icon.ToIconString();
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(iconText);
                    ImGui.PopFont();

                    ImGui.SameLine();

                    using (ImRaii.PushFont(ImGui.GetFont())) ImGui.Text(categoryName.ToUpper());

                    var icon = category.Collapsed
                                       ? FontAwesomeIcon.ChevronDown
                                       : FontAwesomeIcon.ChevronUp;
                    var collapableIconString = icon.ToIconString();


                    ImGui.SameLine(ImGui.GetContentRegionAvail()
                                        .X                -
                                   (30 * GlobalFontScale) -
                                   ImGui.CalcTextSize(collapableIconString)
                                        .X);
                    ImGui.PushFont(UiBuilder.IconFont);

                    if (ImGui.Button(collapableIconString))
                        category.Collapsed = !category.Collapsed;

                    ImGui.PopFont();

                    ImGui.SameLine(ImGui.GetContentRegionAvail()
                                        .X -
                                   (15 * GlobalFontScale));

                    using (ImRaii.PushColor(ImGuiCol.Text, Theme.AccentPink))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(1f, 0.42f, 0.62f, 0.15f)))
                        {
                            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.42f, 0.62f, 0.2f)))
                            {
                                using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(1f, 0.42f, 0.62f, 0.25f)))
                                {
                                    using (ImRaii.PushColor(ImGuiCol.Border, Theme.AccentPink))
                                    {
                                        var style       = ImGui.GetStyle();
                                        var oldRounding = style.FrameRounding;
                                        var oldPadding  = style.FramePadding;
                                        var oldBorder   = style.FrameBorderSize;

                                        style.FrameRounding   = 10f * GlobalFontScale;
                                        style.FramePadding    = new Vector2(8 * GlobalFontScale, 4 * GlobalFontScale);
                                        style.FrameBorderSize = 1f;

                                        var textSize              = ImGui.CalcTextSize($"{category.Items.Count}");
                                        var buttonHeight          = textSize.Y + (style.FramePadding.Y * 2);
                                        var buttonVerticalPadding = (headerHeight - buttonHeight) / 2f;

                                        ImGui.SetCursorPosY(buttonVerticalPadding);

                                        ImGui.Button($"{category.Items.Count}", new Vector2(25 * GlobalFontScale, 0));
                                        style.FrameRounding   = oldRounding;
                                        style.FramePadding    = oldPadding;
                                        style.FrameBorderSize = oldBorder;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (!category.Collapsed)
            foreach (var item in category.Items)
                DrawExpandedNavItem(item);

        ImGui.Spacing();
    }

    private void DrawCollapsedNavItem(NavItem item)
    {
        var isActive = item.Name == ActiveItemName;
        var iconText = item.Icon.ToIconString();

        ImGui.PushFont(UiBuilder.IconFont);
        var textSize = ImGui.CalcTextSize(iconText);
        ImGui.PopFont();

        ImGui.SetCursorPosX((collapsedWidth - textSize.X) / 2);

        var color = isActive
                            ? Theme.AccentPink
                            : Theme.TextPrimary;

        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.PushFont(UiBuilder.IconFont);

            if (ImGui.Selectable($"{iconText}##{item.Name}", isActive, ImGuiSelectableFlags.None, new Vector2(textSize.X, textSize.Y)))
            {
                item.OnClick();
                ActiveItemName = ActiveItemName == item.Name
                                         ? string.Empty
                                         : item.Name;
            }

            ImGui.PopFont();
        }

        if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.Name);
    }

    private void DrawExpandedNavItem(NavItem item)
    {
        var isActive = item.Name == ActiveItemName;
        var iconText = item.Icon.ToIconString();
        var textColor = isActive
                                ? Theme.AccentPink
                                : Theme.TextPrimary;

        var selectableColor = isActive
                                      ? new Vector4(0.20f, 0.20f, 0.20f, 1f)
                                      : Vector4.Zero;

        using (ImRaii.PushColor(ImGuiCol.Header, selectableColor))
        {
            using (ImRaii.PushColor(ImGuiCol.HeaderHovered, Theme.BackgroundCard))
            {
                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                {
                    var clicked = ImGui.Selectable($"##{item.Name}", isActive, ImGuiSelectableFlags.None, new Vector2(0, 25 * GlobalFontScale));

                    var itemRect = ImGui.GetItemRectMin();
                    var itemSize = ImGui.GetItemRectSize();
                    var drawList = ImGui.GetWindowDrawList();

                    if (isActive)
                    {
                        drawList.AddRectFilled(
                                               new Vector2(itemRect.X, itemRect.Y),
                                               new Vector2(itemRect.X + 3, itemRect.Y + itemSize.Y),
                                               ImGui.GetColorU32(Theme.AccentPink)
                                              );
                    }

                    ImGui.SetCursorScreenPos(new Vector2(itemRect.X + (20 * GlobalFontScale), itemRect.Y + (5 * GlobalFontScale)));

                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextUnformatted(iconText);
                    ImGui.PopFont();

                    ImGui.SameLine();
                    ImGui.SetCursorScreenPos(new Vector2(itemRect.X + (45 * GlobalFontScale), itemRect.Y + (5 * GlobalFontScale)));
                    ImGui.TextUnformatted(item.Name);

                    if (clicked)
                    {
                        item.OnClick();
                        ActiveItemName = ActiveItemName == item.Name
                                                 ? string.Empty
                                                 : item.Name;
                    }
                }
            }
        }
    }
}
