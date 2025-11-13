using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Henchman.Windows.Layout;

public enum ColumnAlignment
{
    Left,
    Center,
    Right
}

public record TableColumn<T>(
        string            Name,
        Func<T, string>?  GetValue     = null,
        float             Width        = 0,
        ColumnAlignment   Alignment    = ColumnAlignment.Left,
        Func<T, Vector4>? GetTextColor = null,
        Action<T>?        DrawCustom   = null
);

public class Table<T>(
        string               tableId,
        List<TableColumn<T>> columns,
        Func<IEnumerable<T>> getItems,
        Func<T, bool>?       highlightPredicate = null,
        Vector2              size               = default,
        Action?              drawExtraRow       = null
)
{
    private float GlobalFontScale => ImGui.GetIO()
                                          .FontGlobalScale;

    public void Draw()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg   |
                                      ImGuiTableFlags.Borders |
                                      ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.SizingStretchProp;
        using var table = ImRaii.Table(tableId, columns.Count, flags, size * GlobalFontScale);
        if (!table.Success) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        SetupColumns();
        ImGui.TableHeadersRow();
        DrawRows();
    }

    private void SetupColumns()
    {
        foreach (var column in columns)
        {
            if (column.Width > 0)
                ImGui.TableSetupColumn(column.Name, ImGuiTableColumnFlags.WidthFixed, column.Width * GlobalFontScale);
            else
                ImGui.TableSetupColumn(column.Name, ImGuiTableColumnFlags.WidthStretch);
        }
    }

    private void DrawRows()
    {
        foreach (var item in getItems())
        {
            ImGui.TableNextRow();

            var isHighlighted = highlightPredicate?.Invoke(item) ?? false;
            if (isHighlighted) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(Theme.AccentPinkDim));

            for (var i = 0; i < columns.Count; i++)
            {
                ImGui.TableNextColumn();
                var column = columns[i];

                if (column.DrawCustom != null)
                {
                    if (column.Alignment == ColumnAlignment.Center)
                        DrawCentered($"##Centered{column.Name}{i}", () => column.DrawCustom(item));
                    else
                        column.DrawCustom(item);
                }
                else
                {
                    var value     = column.GetValue?.Invoke(item) ?? "";
                    var textColor = column.GetTextColor?.Invoke(item);
                    DrawCell(value, column.Alignment, textColor);
                }
            }
        }

        drawExtraRow?.Invoke();
    }

    private void DrawCell(string value, ColumnAlignment alignment, Vector4? textColor)
    {
        var isIcon = value.Length == 1 && char.ConvertToUtf32(value, 0) > 0xF000;

        if (isIcon) ImGui.PushFont(UiBuilder.IconFont);

        var textSize = ImGui.CalcTextSize(value);

        if (alignment == ColumnAlignment.Center || alignment == ColumnAlignment.Right)
        {
            var contentWidth = ImGui.GetContentRegionAvail()
                                    .X;
            var offset = alignment == ColumnAlignment.Center
                                 ? (contentWidth - textSize.X) * 0.5f
                                 : contentWidth - textSize.X;

            if (offset > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        }

        if (textColor.HasValue)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, textColor.Value))
                ImGui.Text(value);
        }
        else
            ImGui.Text(value);

        if (isIcon) ImGui.PopFont();
    }
}
