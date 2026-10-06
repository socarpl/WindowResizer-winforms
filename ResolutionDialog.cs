namespace WindowResizer;

public sealed class ResolutionDialog : Form
{
    private readonly NumericUpDown _width = new()
    {
        Minimum = 1,
        Maximum = int.MaxValue,
        AutoSize = false,
        Font = new Font(SystemFonts.DefaultFont.FontFamily, 30, FontStyle.Bold),
        Dock = DockStyle.Fill,
        TextAlign = HorizontalAlignment.Center,
        Margin = new Padding(8, 20, 8, 20)
    };

    private readonly NumericUpDown _height = new()
    {
        Minimum = 1,
        Maximum = int.MaxValue,
        AutoSize = false,
        Font = new Font(SystemFonts.DefaultFont.FontFamily, 30, FontStyle.Bold),
        Dock = DockStyle.Fill,
        TextAlign = HorizontalAlignment.Center,
        Margin = new Padding(8, 20, 8, 20)
    };

    public ResolutionInfo Resolution => new((int)_width.Value, (int)_height.Value);

    public ResolutionDialog(ResolutionInfo? existing = null, Func<ResolutionInfo, bool>? canSave = null)
    {
        Text = existing is null ? "Add Resolution" : "Edit Resolution";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 230);
        AutoScaleMode = AutoScaleMode.Dpi;

        _width.Value = existing?.Width ?? 800;
        _height.Value = existing?.Height ?? 600;

        var resolutionGroup = new GroupBox { Text = "Resolution", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var dimensionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        dimensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        dimensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
        dimensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        dimensionRow.Controls.Add(_width, 0, 0);
        dimensionRow.Controls.Add(new Label
        {
            Text = "×", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 28, FontStyle.Bold)
        }, 1, 0);
        dimensionRow.Controls.Add(_height, 2, 0);
        resolutionGroup.Controls.Add(dimensionRow);

        var save = new Button { Text = "Save", Size = new Size(90, 38) };
        save.Click += (_, _) =>
        {
            if (canSave is not null && !canSave(Resolution))
            {
                MessageBox.Show(this, "This resolution is already in the list.",
                    "Duplicate resolution", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 38) };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.Controls.Add(resolutionGroup, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
