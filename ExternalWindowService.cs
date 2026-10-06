using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowResizer;

public sealed class ExternalWindowService
{
    private readonly Action<string> _log;

    public ExternalWindowService(Action<string> log) => _log = log;

    public IReadOnlyList<WindowInfo> GetWindows()
    {
        var windows = new List<WindowInfo>();
        var seenHandles = new HashSet<IntPtr>();
        var ownPid = (uint)Environment.ProcessId;
        NativeMethods.EnumWindowsProc callback = (hwnd, _) =>
        {
            try
            {
                if (!NativeMethods.IsWindowVisible(hwnd) ||
                    NativeMethods.GetWindowThreadProcessId(hwnd, out var pid) == 0 ||
                    pid == 0 || pid == ownPid || pid > int.MaxValue)
                    return true;

                using var process = Process.GetProcessById((int)pid);
                var titleLength = Math.Clamp(NativeMethods.GetWindowTextLength(hwnd), 0, 32767);
                var title = new StringBuilder(titleLength + 1);
                NativeMethods.GetWindowText(hwnd, title, title.Capacity);
                var className = new StringBuilder(256);
                NativeMethods.GetClassName(hwnd, className, className.Capacity);
                var titleText = title.ToString().Trim();
                if (seenHandles.Add(hwnd))
                    windows.Add(new WindowInfo(hwnd, pid, process.ProcessName, titleText, className.ToString()));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // The process can exit, or access to its metadata can be denied during enumeration.
            }
            return true;
        };

        if (!NativeMethods.EnumWindows(callback, IntPtr.Zero))
            throw NativeError("EnumWindows");

        var sorted = windows
            .OrderBy(window => window.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(window => window.ProcessId)
            .ThenBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(window => window.Handle.ToInt64())
            .ToList();
        _log($"Window refresh: {sorted.Count} distinct visible external window entries found.");
        return sorted;
    }

    public WindowDimensions GetDimensions(WindowInfo window)
    {
        ValidateTarget(window);
        if (!NativeMethods.GetWindowRect(window.Handle, out var outer))
            throw NativeError("GetWindowRect");
        if (!NativeMethods.GetClientRect(window.Handle, out var client))
            throw NativeError("GetClientRect");
        return new WindowDimensions(outer.Width, outer.Height, client.Width, client.Height);
    }

    public void BringToFront(WindowInfo window)
    {
        ValidateTarget(window);
        LogTarget(window);
        RestoreIfNeeded(window, requireNormalState: false);

        var foregroundRequest = NativeMethods.SetForegroundWindow(window.Handle);
        var foreground = NativeMethods.GetForegroundWindow() == window.Handle;
        _log($"SetForegroundWindow returned {foregroundRequest}; target foreground: {foreground}.");
        if (!foreground)
        {
            var topRequest = NativeMethods.BringWindowToTop(window.Handle);
            foregroundRequest = NativeMethods.SetForegroundWindow(window.Handle);
            foreground = NativeMethods.GetForegroundWindow() == window.Handle;
            _log($"BringWindowToTop returned {topRequest}; second SetForegroundWindow returned {foregroundRequest}; target foreground: {foreground}.");
        }

        if (!foreground)
            throw new InvalidOperationException("Windows did not allow this window to take focus. It may be subject to foreground-activation restrictions.");
    }

    public ResizeResult ResizeOuterWindow(WindowInfo window, int width, int height)
        => Resize(window, width, height, ResizeMode.Window);

    public ResizeResult ResizeClientArea(WindowInfo window, int width, int height)
        => Resize(window, width, height, ResizeMode.ContentArea);

    private ResizeResult Resize(WindowInfo window, int width, int height, ResizeMode mode)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive integers.");

        ValidateTarget(window);
        LogTarget(window);
        RestoreIfNeeded(window, requireNormalState: true);

        var before = GetDimensions(window);
        _log($"Current {before}. Requested {mode}: {width}x{height}.");
        var after = before;
        for (var attempt = 1; attempt <= (mode == ResizeMode.ContentArea ? 3 : 1); attempt++)
        {
            var desiredOuterWidth = mode == ResizeMode.Window
                ? width : checked(after.OuterWidth + (width - after.ClientWidth));
            var desiredOuterHeight = mode == ResizeMode.Window
                ? height : checked(after.OuterHeight + (height - after.ClientHeight));
            if (desiredOuterWidth <= 0 || desiredOuterHeight <= 0)
                throw new InvalidOperationException("Calculated outer window dimensions are not positive.");

            _log($"SetWindowPos attempt {attempt}: calculated outer {desiredOuterWidth}x{desiredOuterHeight}.");
            ValidateTarget(window);
            if (!NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero, 0, 0,
                    desiredOuterWidth, desiredOuterHeight,
                    NativeMethods.SwpNoMove | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate))
            {
                var error = Marshal.GetLastWin32Error();
                _log($"SetWindowPos failed (Win32 error {error}).");
                throw NativeError("SetWindowPos", error);
            }

            _log("SetWindowPos returned success.");
            after = GetDimensions(window);
            _log($"Measured {after}.");
            if (mode == ResizeMode.Window
                ? after.OuterWidth == width && after.OuterHeight == height
                : after.ClientWidth == width && after.ClientHeight == height)
                break;
        }

        return new ResizeResult(before, after, width, height, mode);
    }

    private void RestoreIfNeeded(WindowInfo window, bool requireNormalState)
    {
        // A minimized window that was maximized can return to maximized on its first
        // restore. Check the state again and restore once more before measuring/sizing.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            ValidateTarget(window);
            var minimized = NativeMethods.IsIconic(window.Handle);
            var maximized = NativeMethods.IsZoomed(window.Handle);
            if (!minimized && (!requireNormalState || !maximized))
                return;

            _log($"Restore attempt {attempt}: minimized={minimized}, maximized={maximized}.");
            // ShowWindow's return value describes the previous visibility, not whether
            // this call restored the target. Verify with IsIconic/IsZoomed instead.
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        ValidateTarget(window);
        var stillMinimized = NativeMethods.IsIconic(window.Handle);
        var stillMaximized = NativeMethods.IsZoomed(window.Handle);
        if (stillMinimized || (requireNormalState && stillMaximized))
        {
            _log($"Restore did not reach the required state: minimized={stillMinimized}, maximized={stillMaximized}.");
            throw new InvalidOperationException(requireNormalState
                ? "The target window could not be restored to its normal state, so resizing was stopped."
                : "The target window could not be un-minimized, so it cannot be brought to the front.");
        }
    }

    private static void ValidateTarget(WindowInfo window)
    {
        if (!NativeMethods.IsWindow(window.Handle) ||
            NativeMethods.GetWindowThreadProcessId(window.Handle, out var pid) == 0 ||
            pid != window.ProcessId)
            throw new InvalidOperationException("The selected window is no longer available. Refresh the window list.");
    }

    private void LogTarget(WindowInfo window)
        => _log($"Target HWND: 0x{window.Handle.ToInt64():X}; PID: {window.ProcessId}; process: {window.ProcessName}; class: {window.ClassName}.");

    private static Win32Exception NativeError(string operation, int? error = null)
    {
        var code = error ?? Marshal.GetLastWin32Error();
        return new Win32Exception(code, $"{operation} failed (Win32 error {code}). The target may have closed or may be running at a higher privilege level.");
    }
}
