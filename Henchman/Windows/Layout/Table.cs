using System.Linq;
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
        Action<T, int>?   DrawCustom   = null
);

public class Table<T>
{
    private string               TableId            { get; }
    private List<TableColumn<T>> Columns            { get; }
    private Func<IEnumerable<T>> GetItems           { get; }
    private Func<T, bool>?       HighlightPredicate { get; }
    private Vector2              Size               { get; }
    private Action?              DrawExtraRow       { get; }
    private int?                ItemAmountShown    { get; }

    public Table(
            string               tableId,
            List<TableColumn<T>> columns,
            Func<IEnumerable<T>> getItems,
            Func<T, bool>?       highlightPredicate = null,
            Vector2              size               = default,
            Action?              drawExtraRow       = null)
    {
        TableId            = tableId;
        Columns            = columns;
        GetItems           = getItems;
        HighlightPredicate = highlightPredicate;
        Size               = size;
        DrawExtraRow       = drawExtraRow;
    }
    public Table(
            string               tableId,
            List<TableColumn<T>> columns,
            Func<IEnumerable<T>> getItems,
            int                 itemAmountShown,
            Func<T, bool>?       highlightPredicate = null,
            Vector2              size               = default)
    {
        TableId            = tableId;
        Columns            = columns;
        GetItems           = getItems;
        HighlightPredicate = highlightPredicate;
        Size               = size;
        ItemAmountShown    = itemAmountShown;
    }

    private float GlobalFontScale => ImGui.GetIO()
                                          .FontGlobalScale;

    public void Draw()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg   |
                                      ImGuiTableFlags.Borders |
                                      ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.SizingStretchProp;
        using var table = ImRaii.Table(TableId, Columns.Count, flags, Size * GlobalFontScale);
        if (!table.Success) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        SetupColumns();
        ImGui.TableHeadersRow();
        DrawRows();
    }

    private void SetupColumns()
    {
        foreach (var column in Columns)
        {
            if (column.Width > 0)
                ImGui.TableSetupColumn(column.Name, ImGuiTableColumnFlags.WidthFixed, column.Width * GlobalFontScale);
            else
                ImGui.TableSetupColumn(column.Name, ImGuiTableColumnFlags.WidthStretch);
        }
    }

    private void DrawRows()
    {
        var            rowIndex = 0;

        foreach (var item in GetItems())
        {
            if (ItemAmountShown > 0 && rowIndex >= ItemAmountShown) break;
            ImGui.TableNextRow();

            var isHighlighted = HighlightPredicate?.Invoke(item) ?? false;
            if (isHighlighted) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(Theme.AccentPinkDim));

            for (var i = 0; i < Columns.Count; i++)
            {
                ImGui.TableNextColumn();
                var column = Columns[i];

                if (column.DrawCustom != null)
                {
                    if (column.Alignment == ColumnAlignment.Center)
                        DrawCentered($"##Centered{column.Name}{i}", () => column.DrawCustom(item, rowIndex));
                    else
                        column.DrawCustom(item, rowIndex);
                }
                else
                {
                    var value     = column.GetValue?.Invoke(item) ?? "";
                    var textColor = column.GetTextColor?.Invoke(item);
                    DrawCell(value, column.Alignment, textColor);
                }
            }

            rowIndex++;
        }

        DrawExtraRow?.Invoke();
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
