using System.Reflection;
using System.Text.Json;
using System.IO.Compression;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;
using System.Windows.Forms;
using System.Drawing;

internal static class TextOpeningEditorValidation
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static string output = "";
    private static readonly Assembly Manager = Assembly.Load("MalodyVThemeLoader.ThemeManager");
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags)!.GetValue(target)!;
    private static T Field<T>(object target, string name) => (T)Field(target, name);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name, Flags)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetProperty(name, Flags)!.SetValue(target, value);
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
    private static void Require(bool valid, string message) { checks++; if (!valid) throw new InvalidOperationException(message); }
    private static IEnumerable<Control> Walk(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Walk(child)) yield return nested; } }
    private static void Fit(Control root, string stage)
    {
        foreach (var c in Walk(root).Where(c => c.Visible))
        {
            if (c is Button b && b.Text.Length > 0)
            {
                var size = TextRenderer.MeasureText(b.Text.Replace("&", ""), b.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                var inset = (int)Math.Ceiling(12 * b.DeviceDpi / 96d);
                Require(b.ClientSize.Width - 2 * inset - b.Padding.Horizontal >= size.Width && b.ClientSize.Height - b.Padding.Vertical - 4 >= size.Height, stage + ": command clipped: " + b.Text);
                Require(b.Left >= b.Parent!.Padding.Left - 1 && b.Right <= b.Parent.ClientSize.Width - b.Parent.Padding.Right + 1, stage + ": command exceeds parent: " + b.Text);
            }
            if (c is NumberField n)
            {
                var editor = Field<NumericUpDown>(n, "editor"); var viewport = Field<Control>(n, "inputHost"); var minus = Field<Control>(n, "minus"); var plus = Field<Control>(n, "plus");
                Require(n.Height >= Math.Ceiling(32 * n.DeviceDpi / 96d), stage + ": native numeric height");
                Require(!editor.Controls.Cast<Control>().Any(child => child.GetType().Name.Contains("UpDownButtons") && child.Visible), stage + ": native white spinner");
                Require(minus.Left >= viewport.Right && plus.Left >= minus.Right && plus.Right < n.Width, stage + ": steppers overlap entry");
                Require(n.Left >= -1 && n.Right <= n.Parent!.ClientSize.Width + 1, stage + ": numeric field exceeds column");
            }
            if (c.GetType().Name == "SingleLineLabel")
            {
                var size = c.GetPreferredSize(Size.Empty);
                Require(c.Width >= size.Width - 2 && c.Height >= size.Height, stage + ": navigation label clipped: " + c.Text);
            }
        }
    }
    private static void Capture(Control control, string name)
    {
        control.PerformLayout(); Fit(control, name);
        using var bitmap = new Bitmap(control.Width, control.Height); control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size)); bitmap.Save(Path.Combine(output, name + ".png"));
    }
    [STAThread]
    public static int Main(string[] args)
    {
        var fake = args[0]; var appearance = args[1]; output = args[2]; var media = args[3]; Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        AppAppearance.SetDarkMode(appearance == "dark", false);
        var formType = Manager.GetType("MalodyVThemeLoader.ThemeManager.ThemeManagerForm")!;
        var form = (Form)Activator.CreateInstance(formType, new object[] { fake })!;
        var passed = false; string error = "";
        form.Shown += async (_, _) =>
        {
            try
            {
                await Task.Yield();
                Call(form, "Navigate", "resources"); var editor = Field(form, "resourceEditor"); Call(editor, "ShowInspector", true);
                var pane = Field(editor, "inspectorPane"); var workspace = Field(editor, "workspace"); Field<System.Windows.Forms.Timer>(pane, "timer").Stop();
                ThemeResources Rules() => Get<ThemeResources>(workspace, "Rules");
                var root = Get<string>(workspace, "Root"); var json = Path.Combine(root, "resources.json");
                var snapshot = new InspectorSnapshot { Runtime = "text-fixture", ActiveScene = "SceneSong", PluginVersion = "2.6.11", Theme = "TextOpening", SelectedId = 1, SelectionRevision = 1 };
                var node = new InspectorNode { Id = 1, Name = "Text", TextPreview = "GLOBAL / REALTIME / LOCAL", Scene = "SceneSong", Address = "Canvas#0:1/Row#0:2/Text#0:1", Hierarchy = "Canvas/Row/Text", Component = "Text", Active = true, CanDecorate = true, Width = 220, Height = 40 };
                node.Texts.Add(new InspectorText { Component = "Text", Content = node.TextPreview, Color = "#17697B", Opacity = .8f, RichText = true }); snapshot.Nodes.Add(node);
                Call(pane, "AcceptSnapshot", snapshot); var textPane = Field(pane, "textPane");
                Require(((Control)textPane).Visible, "Text color editor not discoverable");
                var normal = Field(textPane, "normal"); Set(normal, "Value", "#DDEEFF"); Set(Field(textPane, "selected"), "Value", "#123456"); Set(Field(textPane, "hover"), "Value", "#FEDCBA");
                Field<ComboBox>(textPane, "scope").SelectedIndex = 1; Field<CheckBox>(textPane, "alphaOverride").Checked = true; Field<NumberField>(textPane, "opacity").Value = 75; Field<ComboBox>(textPane, "rich").SelectedIndex = 1;
                Field<CheckBox>(textPane, "advanced").Checked = true; Field<TextBox>(textPane, "mappings").Text = "#F90000 = #0000EE";
                Require(Field<Button>(pane, "save").Enabled, "Fixed footer cannot save text edits"); Capture(form, "legacy-normal");
                Require((bool)Call(textPane, "Save")!, "Text save failed"); var rule = Rules().Texts.Single();
                Require(rule.Color == "#DDEEFF" && rule.SelectedColor == "#123456" && rule.HoverColor == "#FEDCBA" && rule.RepeatDepth == 1 && rule.Opacity == .75f && rule.RichText == "uniform", "Text state persistence");
                Require(rule.Matches("SceneSong", "Canvas#0:1/Row#4:7/Text#0:1", "Text") && !rule.Matches("SceneStore", "Canvas#0:1/Row#4:7/Text#0:1", "Text"), "Template scope escaped scene");
                Require(rule.ColorFor("#F90000", false, false) == "#0000EE" && rule.ColorFor("#F90000", true, true) == "#FEDCBA", "State precedence");
                var before = File.ReadAllText(json); Set(normal, "Value", "#HHHHHH"); Require(!(bool)Call(textPane, "Save")! && File.ReadAllText(json) == before, "Invalid HEX overwrote rules");
                Require(Field<Label>(pane, "state").Text.Contains("保存失败"), "Fixed footer hid validation error");
                Set(normal, "Value", "#010203"); Field<CheckBox>(normal, "rgbToggle").Checked = true;
                var channels = Field<NumberField[]>(normal, "channels"); channels[0].Value = 255; channels[1].Value = 128; channels[2].Value = 64;
                Require(Get<string>(normal, "Value") == "#FF8040", "RGB did not update HEX"); snapshot.SelectionRevision++;
                Call(pane, "AcceptSnapshot", snapshot); Require(Get<bool>(textPane, "Dirty") && Get<string>(normal, "Value") == "#FF8040", "Snapshot discarded color draft");
                Require((bool)Call(textPane, "Save")!, "RGB save failed"); Call(textPane, "Restore"); Require(Rules().Texts.Count == 0, "Restore retained text rule");
                node.Component = "TMP_Text"; node.Texts = new() { new InspectorText { Component = "TMP_Text", Content = "GRADE", Color = "#FFFFFF" } }; snapshot.SelectionRevision++;
                Call(pane, "AcceptSnapshot", snapshot); Field<CheckBox>(textPane, "advanced").Checked = true; Field<ComboBox>(textPane, "gradient").SelectedIndex = 2;
                var corners = (Array)Field(textPane, "corners"); var colors = new[] { "#FF0000", "#00FF00", "#0000FF", "#FFFFFF" };
                for (var i = 0; i < 4; i++) Set(corners.GetValue(i)!, "Value", colors[i]); Set(Field(textPane, "face"), "Value", "#998877"); Set(Field(textPane, "outline"), "Value", "#123456");
                Require((bool)Call(textPane, "Save")!, "TMP save failed"); Require(Rules().Texts[0].GradientColors[2] == "#0000FF" && Rules().Texts[0].FaceColor == "#998877", "TMP settings missing");
                var scroll = ((Control)textPane).Parent; while (scroll is not Panel { AutoScroll: true }) scroll = scroll!.Parent;
                ((Panel)scroll).ScrollControlIntoView(Field<Control>(textPane, "gradient")); Capture(form, "tmp-advanced");
                Call(editor, "ShowOpening"); var opening = Field(editor, "openingPane"); Capture(form, "opening-empty");
                await (Task)Call(opening, "ImportAsync", Path.Combine(media, "background.png"))!;
                Field<NumberField>(opening, "duration").Value = 5.5m; Field<ComboBox>(opening, "fit").SelectedIndex = 1; Set(Field(opening, "background"), "Value", "#182030");
                await (Task)Call(opening, "SaveAsync")!;
                Require(Rules().Opening is { Kind: "image", Duration: 5.5f, Fit: "cover" }, "Opening image settings");
                Require(Field<Label>(opening, "status").Text.Contains("重启"), "Restart requirement missing"); Capture(form, "opening-image");
                before = File.ReadAllText(json); await (Task)Call(opening, "ImportAsync", Path.Combine(media, "vp9.webm"))!;
                Require(!Get<bool>(opening, "Dirty") && File.ReadAllText(json) == before && Field<Label>(opening, "status").Text.Contains("VP8"), "Invalid video damaged saved opening");
                await (Task)Call(opening, "ImportAsync", Path.Combine(media, "vp8.webm"))!; await (Task)Call(opening, "SaveAsync")!;
                Require(Rules().Opening?.Kind == "video" && Rules().Texts.Count == 1, "Video save discarded text rule"); Capture(form, "opening-video");
                var zipPath = Path.Combine(fake, "export.zip"); if (File.Exists(zipPath)) File.Delete(zipPath); ThemePackage.CreateFromDirectory(root, zipPath);
                using (var zip = ZipFile.OpenRead(zipPath)) Require(zip.GetEntry("images/" + Rules().Opening!.File) != null && zip.GetEntry("resources.json") != null, "ZIP omitted opening/rules");
                Call(opening, "Restore"); Require(Rules().Opening == null && Rules().Texts.Count == 1, "Opening restore removed text rule");
                form.ClientSize = new Size(1120, 800); Capture(form, "minimum");
                foreach (var factor in new[] { 1.25f, 1.5f, 2f })
                {
                    using var layoutForm = (Form)Activator.CreateInstance(formType, new object[] { fake })!; layoutForm.Show();
                    Call(layoutForm, "Navigate", "resources"); var layoutEditor = Field(layoutForm, "resourceEditor"); Call(layoutEditor, "ShowOpening"); layoutForm.Scale(new SizeF(factor, factor));
                    using var layoutCanvas = new Panel { Font = layoutForm.Font, BackColor = layoutForm.BackColor, Size = new Size((int)(1120 * factor), (int)(800 * factor)) };
                    foreach (var section in layoutForm.Controls.Cast<Control>().ToArray()) layoutCanvas.Controls.Add(section);
                    layoutCanvas.CreateControl(); layoutCanvas.PerformLayout(); Capture(layoutCanvas, "scale" + factor);
                }
                using (var fontForm = (Form)Activator.CreateInstance(formType, new object[] { fake })!)
                {
                    fontForm.Show(); Call(fontForm, "Navigate", "resources"); Call(Field(fontForm, "resourceEditor"), "ShowOpening"); fontForm.Size = fontForm.MinimumSize;
                    var fonts = Walk(fontForm).Prepend(fontForm).Select(c => (Control: c, Font: c.Font)).ToArray();
                    foreach (var item in fonts) item.Control.Font = new Font(item.Font.FontFamily, item.Font.Size * 1.25f, item.Font.Style);
                    Call(fontForm, "FitNavigation"); Capture(fontForm, "font125-minimum");
                }
                passed = true;
            }
            catch (Exception e) { error = e.ToString(); }
            finally { form.Close(); Application.ExitThread(); }
        };
        Application.Run(form); form.Dispose();
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed, checks, error, runtime = Environment.Version.ToString(), appearance, scaling = "synthetic 125/150/200%; physical monitor DPI not tested" }));
        return passed ? 0 : 1;
    }
}
