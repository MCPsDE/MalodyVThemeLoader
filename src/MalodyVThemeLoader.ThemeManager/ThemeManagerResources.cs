using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeManagerForm
{
    private ThemeResourceEditor? resourceEditor;
    private readonly ChoiceBox resourceThemes = new() { Dock = DockStyle.Fill, DisplayMember = nameof(ThemeItem.DisplayName) };
    private bool updatingResourceThemes;

    private Control BuildResourcesPage()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(24, 20, 24, 20), Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); page.RowStyles.Add(new RowStyle(SizeType.AutoSize)); page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 16) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        header.Controls.Add(AppVisuals.TextBlock("资源编辑", 22F, true), 0, 0); header.SetRowSpan(header.GetControlFromPosition(0, 0)!, 2);
        header.Controls.Add(AppVisuals.TextBlock("编辑主题", 9F), 1, 0); header.Controls.Add(resourceThemes, 1, 1);
        resourceThemes.AccessibleName = "选择要编辑资源的主题";
        resourceEditor = new ThemeResourceEditor(gameRoot, library, ShowStatus) { Dock = DockStyle.Fill, Margin = Padding.Empty };
        resourceEditor.Applied += () => { RefreshRealtimeMode(); RefreshThemes(resourceEditor.ThemeKey); };
        resourceThemes.SelectedIndexChanged += (_, _) =>
        {
            if (updatingResourceThemes) return;
            var previous = resourceEditor.ThemeKey;
            if (!resourceEditor.SetTheme(resourceThemes.SelectedItem as ThemeItem))
            {
                updatingResourceThemes = true;
                resourceThemes.SelectedItem = resourceThemes.Items.Cast<ThemeItem>().FirstOrDefault(t => t.Key == previous);
                updatingResourceThemes = false;
            }
        };
        page.Controls.Add(header, 0, 0); page.Controls.Add(resourceEditor, 0, 1);
        return page;
    }

    private void RefreshResourceThemes()
    {
        if (resourceEditor == null || updatingResourceThemes) return;
        var previous = resourceEditor.ThemeKey ?? SelectedTheme?.Key;
        updatingResourceThemes = true;
        try
        {
            resourceThemes.Items.Clear();
            foreach (var item in library.GetThemes().Where(t => t.IsValid && !t.IsVirtual)) resourceThemes.Items.Add(item);
            resourceThemes.SelectedItem = resourceThemes.Items.Cast<ThemeItem>().FirstOrDefault(t => t.Key == previous);
            if (resourceThemes.SelectedIndex < 0 && resourceThemes.Items.Count > 0) resourceThemes.SelectedIndex = 0;
        }
        finally { updatingResourceThemes = false; }
        resourceEditor.SetTheme(resourceThemes.SelectedItem as ThemeItem);
    }
}
