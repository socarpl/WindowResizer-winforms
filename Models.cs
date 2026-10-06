namespace WindowResizer;

public sealed record WindowInfo(IntPtr Handle, uint ProcessId, string ProcessName, string Title, string ClassName)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Title)
        ? $"{ProcessName} - {ProcessId} - HWND 0x{Handle.ToInt64():X8}"
        : $"{ProcessName} - {Title.Trim()} - {ProcessId} - HWND 0x{Handle.ToInt64():X8}";
}

public sealed record ResolutionInfo(int Width, int Height)
{
    public static IComparer<ResolutionInfo> ByWidthThenHeight { get; } =
        Comparer<ResolutionInfo>.Create((left, right) =>
        {
            var widthOrder = left.Width.CompareTo(right.Width);
            return widthOrder != 0 ? widthOrder : left.Height.CompareTo(right.Height);
        });

    public override string ToString() => $"{Width} x {Height}";
}

public readonly record struct WindowDimensions(int OuterWidth, int OuterHeight, int ClientWidth, int ClientHeight)
{
    public override string ToString() => $"outer {OuterWidth}x{OuterHeight}, client {ClientWidth}x{ClientHeight}";
}

public enum ResizeMode
{
    Window,
    ContentArea
}

public sealed record ResizeResult(WindowDimensions Before, WindowDimensions After, int RequestedWidth, int RequestedHeight, ResizeMode Mode)
{
    public int ActualWidth => Mode == ResizeMode.Window ? After.OuterWidth : After.ClientWidth;
    public int ActualHeight => Mode == ResizeMode.Window ? After.OuterHeight : After.ClientHeight;
    public bool Achieved => ActualWidth == RequestedWidth && ActualHeight == RequestedHeight;
}
