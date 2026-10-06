# Window Resizer

A .NET 8 WinForms app for resizing visible windows owned by other processes. It supports outer-window and Win32 client-area dimensions.

## Run

On Windows with the .NET 8 Desktop Runtime installed:

```powershell
dotnet run --project WindowResizer.csproj
```

Select a window and a resolution, choose the **Window** or **Content Area** radio button, then click **Set Resolution**. Double-click a resolution to run the same command. Double-click a window to request foreground activation. The filter above the window list searches process names and titles as you type; use the brush icon to clear it or the refresh icon to re-enumerate windows.

Use the plus, pen, and trashcan icons to add, edit, and delete resolutions. The resolution list and mode are saved in `application_settings.json` next to the executable. The app needs write access to that folder to save changes. Diagnostics appear in the lower group box.

Windows can restrict foreground activation and can refuse specific sizes. The app measures the resulting size and reports when a request was not achieved.
