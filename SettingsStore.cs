using System.Text.Json;

namespace WindowResizer;

public sealed class AppSettings
{
    public List<ResolutionInfo> Resolutions { get; set; } = [];
    public ResizeMode ApplyMode { get; set; } = ResizeMode.Window;

    public static AppSettings Defaults() => new()
    {
        Resolutions =
        [
            new(800, 600), new(1024, 768), new(1280, 720),
            new(1920, 1080), new(1920, 1200),
            new(2560, 1440), new(3840, 2160)
        ]
    };
}

public sealed class SettingsStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "application_settings.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(_path))
            return AppSettings.Defaults();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _options)
                ?? throw new InvalidDataException("The settings file is empty.");
            if (settings.Resolutions is null || settings.Resolutions.Any(r => r is null || r.Width <= 0 || r.Height <= 0))
                throw new InvalidDataException("The saved resolution list contains invalid entries.");
            if (!Enum.IsDefined(settings.ApplyMode))
                throw new InvalidDataException("The saved resize mode is invalid.");
            var normalized = settings.Resolutions
                .Distinct()
                .OrderBy(resolution => resolution, ResolutionInfo.ByWidthThenHeight)
                .ToList();
            if (!settings.Resolutions.SequenceEqual(normalized))
            {
                settings.Resolutions = normalized;
                try
                {
                    Save(settings);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warning = $"Resolutions were deduplicated and sorted in memory, but application_settings.json could not be updated: {ex.Message}";
                }
            }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            warning = $"Could not load application_settings.json: {ex.Message} Defaults were loaded.";
            return AppSettings.Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        var tempPath = _path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, _options));
            File.Move(tempPath, _path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
