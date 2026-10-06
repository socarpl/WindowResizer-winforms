using System.ComponentModel;

namespace WindowResizer;

public sealed class MainForm : Form
{
    private readonly ListBox _windows = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One, HorizontalScrollbar = true };
    private readonly ListBox _resolutions = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One, HorizontalScrollbar = true };
    private readonly TextBox _filter = new() { Dock = DockStyle.Fill, Text = "Filter", ForeColor = Color.Gray, Font = new Font(SystemFonts.DefaultFont.FontFamily, 11) };
    private readonly RadioButton _windowMode = new() { Text = "Window", AutoSize = true };
    private readonly RadioButton _contentMode = new() { Text = "Content Area", AutoSize = true };
    private readonly Button _apply = new()
    {
        Text = "Set Resolution", Dock = DockStyle.Fill, Image = IconFactory.Create(AppIcon.Apply),
        TextImageRelation = TextImageRelation.ImageAboveText, ImageAlign = ContentAlignment.MiddleCenter,
        TextAlign = ContentAlignment.MiddleCenter, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
    };
    private readonly Button _clearFilter = IconButton(AppIcon.Clear, "Clear filter");
    private readonly Button _refresh = IconButton(AppIcon.Refresh, "Refresh window list");
    private readonly Button _add = IconButton(AppIcon.Add, "Add resolution");
    private readonly Button _edit = IconButton(AppIcon.Edit, "Edit resolution");
    private readonly Button _delete = IconButton(AppIcon.Delete, "Delete resolution");
    private readonly ToolTip _toolTips = new();
    private readonly TextBox _diagnostics = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font(FontFamily.GenericMonospace, 9)
    };
    private readonly ToolStripStatusLabel _status = new() { Text = "Ready" };
    private readonly SettingsStore _settingsStore = new();
    private readonly ExternalWindowService _windowService;
    private readonly AppSettings _settings;
    private IReadOnlyList<WindowInfo> _allWindows = [];
    private bool _showingFilterPlaceholder = true;

    public MainForm()
    {
        _settings = _settingsStore.Load(out var settingsWarning);
        _windowService = new ExternalWindowService(Log);
        Text = "Window Resizer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 760);
        MinimumSize = new Size(760, 570);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();

        foreach (var resolution in _settings.Resolutions)
            _resolutions.Items.Add(resolution);
        _windowMode.Checked = _settings.ApplyMode == ResizeMode.Window;
        _contentMode.Checked = _settings.ApplyMode == ResizeMode.ContentArea;
        WireEvents();
        UpdateActions();
        if (settingsWarning is not null)
            Log(settingsWarning);
        Shown += (_, _) =>
        {
            ActiveControl = _resolutions;
            RefreshWindows();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        var windowsGroup = new GroupBox { Text = "Windows list", Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0, 0, 6, 8) };
        var windowsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        windowsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        windowsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        _filter.Margin = new Padding(0, 3, 6, 7);
        _clearFilter.Margin = new Padding(0, 2, 5, 7);
        _refresh.Margin = new Padding(0, 2, 0, 7);
        filterRow.Controls.Add(_filter, 0, 0);
        filterRow.Controls.Add(_clearFilter, 1, 0);
        filterRow.Controls.Add(_refresh, 2, 0);
        windowsLayout.Controls.Add(filterRow, 0, 0);
        windowsLayout.Controls.Add(_windows, 0, 1);
        windowsGroup.Controls.Add(windowsLayout);

        var resolutionsGroup = new GroupBox { Text = "Resolutions", Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(6, 0, 0, 8) };
        var resolutionsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        resolutionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        resolutionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resolutionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        resolutionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        actions.Controls.Add(_add);
        actions.Controls.Add(_edit);
        actions.Controls.Add(_delete);
        resolutionsLayout.Controls.Add(actions, 0, 0);
        resolutionsLayout.Controls.Add(_resolutions, 0, 1);

        var modeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        modeRow.Controls.Add(new Label { Text = "Apply resolution to:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        radios.Controls.Add(_windowMode);
        radios.Controls.Add(_contentMode);
        modeRow.Controls.Add(radios, 1, 0);
        resolutionsLayout.Controls.Add(modeRow, 0, 2);
        resolutionsLayout.Controls.Add(_apply, 0, 3);
        resolutionsGroup.Controls.Add(resolutionsLayout);

        columns.Controls.Add(windowsGroup, 0, 0);
        columns.Controls.Add(resolutionsGroup, 1, 0);
        root.Controls.Add(columns, 0, 0);

        var diagnosticsGroup = new GroupBox { Text = "Diagnostics", Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0) };
        diagnosticsGroup.Controls.Add(_diagnostics);
        root.Controls.Add(diagnosticsGroup, 0, 1);
        Controls.Add(root);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);
        Controls.Add(statusStrip);

        _toolTips.SetToolTip(_clearFilter, "Clear filter");
        _toolTips.SetToolTip(_refresh, "Refresh window list");
        _toolTips.SetToolTip(_add, "Add resolution");
        _toolTips.SetToolTip(_edit, "Edit resolution");
        _toolTips.SetToolTip(_delete, "Delete resolution");
    }

    private void WireEvents()
    {
        _windows.SelectedIndexChanged += (_, _) => UpdateActions();
        _resolutions.SelectedIndexChanged += (_, _) => UpdateActions();
        _filter.Enter += (_, _) =>
        {
            if (!_showingFilterPlaceholder)
                return;
            _showingFilterPlaceholder = false;
            _filter.Clear();
            _filter.ForeColor = SystemColors.WindowText;
        };
        _filter.Leave += (_, _) =>
        {
            if (_filter.TextLength != 0)
                return;
            _showingFilterPlaceholder = true;
            _filter.ForeColor = Color.Gray;
            _filter.Text = "Filter";
        };
        _filter.TextChanged += (_, _) => ApplyWindowFilter(_windows.SelectedItem as WindowInfo);
        _clearFilter.Click += (_, _) =>
        {
            _filter.Focus();
            _filter.Clear();
        };
        _refresh.Click += (_, _) => RefreshWindows();
        _add.Click += (_, _) => AddResolution();
        _edit.Click += (_, _) => EditResolution();
        _delete.Click += (_, _) => DeleteResolution();
        _windows.MouseDoubleClick += (_, e) =>
        {
            var index = _windows.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                _windows.SelectedIndex = index;
                BringSelectedToFront();
            }
        };
        _resolutions.MouseDoubleClick += (_, e) =>
        {
            var index = _resolutions.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                _resolutions.SelectedIndex = index;
                ApplySelectedResolution();
            }
        };
        _apply.Click += (_, _) => ApplySelectedResolution();
        _windowMode.CheckedChanged += (_, _) =>
        {
            if (!_windowMode.Checked)
                return;
            _settings.ApplyMode = ResizeMode.Window;
            TrySaveSettings();
        };
        _contentMode.CheckedChanged += (_, _) =>
        {
            if (!_contentMode.Checked)
                return;
            _settings.ApplyMode = ResizeMode.ContentArea;
            TrySaveSettings();
        };
    }

    private void RefreshWindows()
    {
        var previous = _windows.SelectedItem as WindowInfo;
        try
        {
            _allWindows = _windowService.GetWindows();
            ApplyWindowFilter(previous);
            SetStatus($"Found {_allWindows.Count} external windows.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ShowError("Could not refresh the window list", ex);
        }
        UpdateActions();
    }

    private void ApplyWindowFilter(WindowInfo? previouslySelected)
    {
        var query = _showingFilterPlaceholder ? "" : _filter.Text.Trim();
        _windows.BeginUpdate();
        try
        {
            _windows.Items.Clear();
            foreach (var window in _allWindows)
            {
                if (query.Length == 0 ||
                    window.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    window.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                    _windows.Items.Add(window);
            }

            if (previouslySelected is not null)
            {
                for (var i = 0; i < _windows.Items.Count; i++)
                {
                    var current = (WindowInfo)_windows.Items[i]!;
                    if (current.Handle == previouslySelected.Handle && current.ProcessId == previouslySelected.ProcessId)
                    {
                        _windows.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        finally
        {
            _windows.EndUpdate();
        }
        UpdateActions();
    }

    private void AddResolution()
    {
        using var dialog = new ResolutionDialog(canSave: resolution => !_settings.Resolutions.Contains(resolution));
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var resolution = dialog.Resolution;
        _settings.Resolutions.Add(resolution);
        RebuildResolutionList(resolution);
        TrySaveSettings();
        SetStatus($"Added {resolution}.");
    }

    private void EditResolution()
    {
        if (_resolutions.SelectedItem is not ResolutionInfo original)
            return;
        var index = _resolutions.SelectedIndex;
        using var dialog = new ResolutionDialog(original,
            resolution => resolution == original || !_settings.Resolutions.Contains(resolution));
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var updated = dialog.Resolution;
        _settings.Resolutions[index] = updated;
        RebuildResolutionList(updated);
        TrySaveSettings();
        SetStatus($"Updated {original} to {updated}.");
    }

    private void DeleteResolution()
    {
        if (_resolutions.SelectedItem is not ResolutionInfo resolution)
            return;
        var index = _resolutions.SelectedIndex;
        _settings.Resolutions.RemoveAt(index);
        _resolutions.Items.RemoveAt(index);
        TrySaveSettings();
        UpdateActions();
        SetStatus($"Deleted {resolution}.");
    }

    private void RebuildResolutionList(ResolutionInfo selected)
    {
        _settings.Resolutions.Sort(ResolutionInfo.ByWidthThenHeight);
        _resolutions.BeginUpdate();
        try
        {
            _resolutions.Items.Clear();
            foreach (var resolution in _settings.Resolutions)
                _resolutions.Items.Add(resolution);
            _resolutions.SelectedItem = selected;
        }
        finally
        {
            _resolutions.EndUpdate();
        }
    }

    private void BringSelectedToFront()
    {
        if (_windows.SelectedItem is not WindowInfo window)
            return;
        try
        {
            _windowService.BringToFront(window);
            SetStatus($"Brought {window} to the front.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            ShowError("Could not bring the selected window to the front", ex);
        }
    }

    private void ApplySelectedResolution()
    {
        if (_windows.SelectedItem is not WindowInfo window ||
            _resolutions.SelectedItem is not ResolutionInfo resolution ||
            _windows.Items.Count == 0 || _resolutions.Items.Count == 0)
            return;

        try
        {
            var result = _contentMode.Checked
                ? _windowService.ResizeClientArea(window, resolution.Width, resolution.Height)
                : _windowService.ResizeOuterWindow(window, resolution.Width, resolution.Height);
            if (result.Achieved)
            {
                SetStatus($"Applied {resolution} to {window} ({(_contentMode.Checked ? "Content Area" : "Window")}).");
            }
            else
            {
                var area = result.Mode == ResizeMode.Window ? "outer window" : "client area";
                var message = $"Requested {area}: {resolution.Width} x {resolution.Height}.\n" +
                              $"Actual {area}: {result.ActualWidth} x {result.ActualHeight}.\n\n" +
                              "The target application prevented the requested size.";
                Log(message.Replace("\n", " "));
                SetStatus("The target did not reach the requested size.");
                MessageBox.Show(this, message, "Size not achieved", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentOutOfRangeException or OverflowException)
        {
            ShowError("Could not resize the selected window", ex);
        }
    }

    private void UpdateActions()
    {
        _apply.Enabled = _windows.SelectedItem is WindowInfo && _resolutions.SelectedItem is ResolutionInfo;
        _edit.Enabled = _resolutions.SelectedItem is ResolutionInfo;
        _delete.Enabled = _resolutions.SelectedItem is ResolutionInfo;
    }

    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Could not save application_settings.json beside the app", ex);
        }
    }

    private void Log(string text)
    {
        _diagnostics.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        if (_diagnostics.TextLength > 60000)
            _diagnostics.Text = _diagnostics.Text[^40000..];
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        Log(text);
    }

    private void ShowError(string context, Exception ex)
    {
        var message = $"{context}: {ex.Message}";
        SetStatus(message);
        MessageBox.Show(this, message, "Window Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static Button IconButton(AppIcon icon, string accessibleName) => new()
    {
        Image = IconFactory.Create(icon),
        Size = new Size(42, 40),
        ImageAlign = ContentAlignment.MiddleCenter,
        AccessibleName = accessibleName,
        Margin = new Padding(0, 2, 6, 4)
    };
}
