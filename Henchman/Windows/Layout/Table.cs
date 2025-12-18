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
        string              Name,
        Func<T, string>?    GetValue     = null,
        float               Width        = 0,
        FilterType? FilterType   = null,
        ColumnAlignment     Alignment    = ColumnAlignment.Left,
        Func<T, Vector4>?   GetTextColor = null,
        Action<T, int>?     DrawCustom   = null
)
{
    public string          FilterText     { get; set; } = string.Empty;
    public HashSet<string> SelectedValues { get; }      = new();
}

public class Table<T>
{
    private string               TableId            { get; }
    private List<TableColumn<T>> Columns            { get; }
    private Func<IEnumerable<T>> GetItems           { get; }
    private Func<T, bool>?       HighlightPredicate { get; }
    private Vector2              Size               { get; }
    private Action?              DrawExtraRow       { get; }
    private int?                 ItemAmountShown    { get; }

    internal List<T> FilteredItems { get; private set; } = [];

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
            int                  itemAmountShown,
            Func<T, bool>?       highlightPredicate = null,
            Vector2              size               = default)
    {
        TableId            = tableId;
        Columns            = columns;
        GetItems           = getItems;
        ItemAmountShown    = itemAmountShown;
        HighlightPredicate = highlightPredicate;
        Size               = size;
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
            for (var i = 0; i < Columns.Count; i++)
            {
                ImGui.TableSetColumnIndex(i);
                
                var              col    = Columns[i];
                var filter = col.FilterText;
                if(col.FilterType == FilterType.String)
                {
                    if (string.IsNullOrEmpty(filter))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
                        ImGui.SetNextItemWidth(col.Width);
                        if(ImGui.InputTextWithHint($"##Filter_{col.Name}", col.Name, ref filter, 256))
                            col.FilterText = filter;
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.SetNextItemWidth(col.Width);
                        if (ImGui.InputText($"##Filter_{col.Name}", ref filter, 256))
                            col.FilterText = filter;
                    }
                } else if (col.FilterType == FilterType.MultiSelect)
                {
                    var distinctValues = GetItems()
                                        .Select(item => col.GetValue?.Invoke(item) ?? "")
                                        .Distinct()
                                        .OrderBy(v => v)
                                        .ToList();

                    
                    var label = col.SelectedValues.Count == 0 ? "All" : $"{col.SelectedValues.Count} selected";
                    ImGui.SetNextItemWidth(col.Width);
                    if (ImGui.BeginCombo($"##Filter_{col.Name}", label, ImGuiComboFlags.NoArrowButton))
                    {
                        var all = col.SelectedValues.Count == 0;
                        if (ImGui.Checkbox("All", ref all))
                        {
                            col.SelectedValues.Clear();
                        }

                        ImGui.Indent();
                        foreach (var val in distinctValues)
                        {
                            var selected = col.SelectedValues.Contains(val);
                            if (ImGui.Checkbox(val, ref selected))
                            {
                                if (selected)
                                    col.SelectedValues.Add(val);
                                else
                                    col.SelectedValues.Remove(val);
                            }
                        }
                        ImGui.Unindent();

                        ImGui.EndCombo();
                    }
}
        }
        
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
        FilteredItems = [];
        foreach (var item in GetItems())
        {
            if (ItemAmountShown > 0 && rowIndex >= ItemAmountShown)
                break;

            var matches = true;
            foreach (var column in Columns)
            {
                    var value = column.GetValue?.Invoke(item);

                    if (column.FilterType == FilterType.String && !string.IsNullOrEmpty(column.FilterText))
                    {
                        if (value == null || !value.Contains(column.FilterText, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    else if (column is { FilterType: FilterType.MultiSelect, SelectedValues.Count: > 0 })
                    {
                        if (!column.SelectedValues.Contains(value))
                        {
                            matches = false;
                            break;
                        }
                    }
            }

            if (!matches)
                continue; 

            FilteredItems.Add(item);
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

public enum FilterType
{
    String,
    MultiSelect
}
