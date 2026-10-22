namespace MalodyVThemeLoader.Windows;

/// <summary>Equal-width fields that stack before their inputs become too narrow.</summary>
public sealed class FieldGrid : TableLayoutPanel
{
    private readonly List<Control> fields = new();
    private readonly int maximumColumns;
    private bool arranging;
    private int Gap => (int)Math.Ceiling(10 * DeviceDpi / 96f);
    public FieldGrid(int columns = 2)
    {
        maximumColumns = columns; ColumnCount = columns;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Margin = Padding.Empty;
    }
    public void AddField(Control control)
    {
        fields.Add(control); control.Dock = DockStyle.Top; Controls.Add(control);
        PerformLayout();
    }
    private int ColumnsFor(int width)
    {
        if (fields.Count == 0) return 1;
        var minimum = fields.Max(c => c.GetPreferredSize(Size.Empty).Width);
        var count = Math.Min(maximumColumns, fields.Count);
        while (count > 1 && count * minimum + (count - 1) * Gap > width) count--;
        return count;
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (fields == null || fields.Count == 0) return Size.Empty;
        var available = Math.Max(1, (proposedSize.Width > 0 ? proposedSize.Width : Width) - Padding.Horizontal);
        var columns = ColumnsFor(available);
        var cellWidth = Math.Max(1, (available - (columns - 1) * Gap) / columns);
        var height = 0;
        for (var i = 0; i < fields.Count; i += columns)
            height += fields.Skip(i).Take(columns).Max(c => c.GetPreferredSize(new Size(cellWidth, 0)).Height) + (i > 0 ? Gap : 0);
        var width = fields.Max(c => c.GetPreferredSize(Size.Empty).Width) * columns + (columns - 1) * Gap;
        return new Size(width + Padding.Horizontal, height + Padding.Vertical);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging || fields == null || fields.Count == 0) return;
        arranging = true;
        try
        {
            var columns = ColumnsFor(Math.Max(1, ClientSize.Width - Padding.Horizontal));
            var rows = (fields.Count + columns - 1) / columns;
            var tracks = columns * 2 - 1;
            if (ColumnCount != tracks || RowCount != rows || ColumnStyles.Count != tracks || RowStyles.Count != rows)
            {
                ColumnCount = tracks; RowCount = rows;
                ColumnStyles.Clear(); RowStyles.Clear();
                for (var i = 0; i < tracks; i++) ColumnStyles.Add(i % 2 == 0
                    ? new ColumnStyle(SizeType.Percent, 100f / columns) : new ColumnStyle(SizeType.Absolute, Gap));
                for (var i = 0; i < rows; i++) RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            for (var i = 0; i < fields.Count; i++)
            {
                var cell = new TableLayoutPanelCellPosition(i % columns * 2, i / columns);
                if (GetCellPosition(fields[i]) != cell) SetCellPosition(fields[i], cell);
                var margin = new Padding(0, 0, 0, i / columns < rows - 1 ? Gap : 0);
                if (fields[i].Margin != margin) fields[i].Margin = margin;
            }
        }
        finally { arranging = false; }
        base.OnLayout(e);
    }
}
