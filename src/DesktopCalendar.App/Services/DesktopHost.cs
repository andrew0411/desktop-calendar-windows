using System.Runtime.InteropServices;

namespace DesktopCalendar.App.Services;

public sealed class DesktopHost : IDisposable
{
    private nint _windowHandle;
    private nint _progman;
    private nint _desktopAnchor;
    private bool _layoutEditing;

    public bool IsAttached { get; private set; }

    public bool Attach(nint windowHandle, int x, int y, int width, int height)
    {
        _windowHandle = windowHandle;
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == nint.Zero || !NativeMethods.IsWindow(progman))
        {
            NativeMethods.ShowWindow(windowHandle, NativeMethods.SwHide);
            _progman = nint.Zero;
            _desktopAnchor = nint.Zero;
            SetAttached(false);
            return false;
        }

        // WPF transparent HWNDs do not render reliably as cross-process Progman
        // children. Keep the window in the top-level band immediately above
        // Progman and below every ordinary application window instead.
        NativeMethods.SetParent(windowHandle, nint.Zero);
        var style = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlStyle).ToInt64();
        style &= ~(NativeMethods.WsChild | NativeMethods.WsCaption | NativeMethods.WsThickFrame);
        style |= NativeMethods.WsPopup | NativeMethods.WsVisible;
        if (_layoutEditing)
            style |= NativeMethods.WsThickFrame;
        NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GwlStyle, new nint(style));
        // ShowInTaskbar=False gives a WPF window an invisible owner. Clear it so
        // closing another WPF window cannot hide the calendar along with it.
        NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GwlHwndParent, nint.Zero);

        var exStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle).ToInt64();
        exStyle |= NativeMethods.WsExToolWindow;
        exStyle &= ~(NativeMethods.WsExTopmost | NativeMethods.WsExNoActivate | NativeMethods.WsExAppWindow);
        NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle, new nint(exStyle));

        var anchor = NativeMethods.GetWindow(progman, NativeMethods.GwHwndPrev);
        if (anchor == nint.Zero)
            anchor = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (anchor == nint.Zero)
        {
            NativeMethods.ShowWindow(windowHandle, NativeMethods.SwHide);
            _progman = nint.Zero;
            _desktopAnchor = nint.Zero;
            SetAttached(false);
            return false;
        }

        _progman = progman;
        _desktopAnchor = anchor;
        var flags = NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpFrameChanged;
        if (anchor == windowHandle)
            flags |= NativeMethods.SwpNoZOrder;
        var positioned = NativeMethods.SetWindowPos(
            windowHandle,
            anchor == windowHandle ? nint.Zero : anchor,
            x, y, Math.Max(320, width), Math.Max(240, height),
            flags);
        if (positioned)
            NativeMethods.ShowWindow(windowHandle, NativeMethods.SwShowNoActivate);
        SetAttached(positioned);
        return positioned;
    }

    public void SetLayoutEditing(bool enabled)
    {
        _layoutEditing = enabled;
        if (_windowHandle == nint.Zero || !NativeMethods.IsWindow(_windowHandle))
            return;
        var style = NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlStyle).ToInt64();
        style = enabled ? style | NativeMethods.WsThickFrame : style & ~NativeMethods.WsThickFrame;
        NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GwlStyle, new nint(style));
        NativeMethods.SetWindowPos(
            _windowHandle, nint.Zero, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
    }

    public void BeginSystemDrag(string direction)
    {
        if (_windowHandle == nint.Zero || !NativeMethods.IsWindow(_windowHandle))
            return;
        var hitTest = direction switch
        {
            "Move" => NativeMethods.HtCaption,
            "Left" => NativeMethods.HtLeft,
            "Right" => NativeMethods.HtRight,
            "Top" => NativeMethods.HtTop,
            "Bottom" => NativeMethods.HtBottom,
            "TopLeft" => NativeMethods.HtTopLeft,
            "TopRight" => NativeMethods.HtTopRight,
            "BottomLeft" => NativeMethods.HtBottomLeft,
            "BottomRight" => NativeMethods.HtBottomRight,
            _ => 0
        };
        if (hitTest == 0)
            return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(_windowHandle, NativeMethods.WmNcLeftButtonDown, new nint(hitTest), nint.Zero);
    }

    public bool RestoreZOrder()
    {
        if (_windowHandle == nint.Zero || !NativeMethods.IsWindow(_windowHandle))
            return false;
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == nint.Zero || !NativeMethods.IsWindow(progman))
            return false;
        var anchor = NativeMethods.GetWindow(progman, NativeMethods.GwHwndPrev);
        if (anchor == nint.Zero)
            anchor = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (anchor == nint.Zero)
            return false;

        _progman = progman;
        _desktopAnchor = anchor;
        if (anchor == _windowHandle)
            return true;
        return NativeMethods.SetWindowPos(
            _windowHandle,
            anchor,
            0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    public bool TryGetWindowBounds(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (_windowHandle == nint.Zero || !NativeMethods.IsWindow(_windowHandle) ||
            !NativeMethods.GetWindowRect(_windowHandle, out var rect))
            return false;
        x = rect.Left;
        y = rect.Top;
        width = rect.Right - rect.Left;
        height = rect.Bottom - rect.Top;
        return width > 0 && height > 0;
    }

    public void Detach()
    {
        if (_windowHandle != nint.Zero && NativeMethods.IsWindow(_windowHandle))
            NativeMethods.ShowWindow(_windowHandle, NativeMethods.SwHide);
        _progman = nint.Zero;
        _desktopAnchor = nint.Zero;
        SetAttached(false);
    }

    public bool IsDesktopAvailable() =>
        _windowHandle != nint.Zero &&
        _progman != nint.Zero &&
        NativeMethods.IsWindow(_windowHandle) &&
        NativeMethods.IsWindow(_progman) &&
        NativeMethods.IsWindowVisible(_windowHandle) &&
        NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlHwndParent) == nint.Zero &&
        (NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTopmost) == 0;

    public void Dispose() => Detach();

    private void SetAttached(bool value) => IsAttached = value;

    private static class NativeMethods
    {
        internal const int GwlStyle = -16;
        internal const int GwlExStyle = -20;
        internal const int GwlHwndParent = -8;
        internal const long WsChild = 0x40000000L;
        internal const long WsVisible = 0x10000000L;
        internal const long WsPopup = 0x80000000L;
        internal const long WsCaption = 0x00C00000L;
        internal const long WsThickFrame = 0x00040000L;
        internal const long WsExTopmost = 0x00000008L;
        internal const long WsExToolWindow = 0x00000080L;
        internal const long WsExAppWindow = 0x00040000L;
        internal const long WsExNoActivate = 0x08000000L;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpNoSize = 0x0001;
        internal const uint SwpNoMove = 0x0002;
        internal const uint SwpNoZOrder = 0x0004;
        internal const uint SwpShowWindow = 0x0040;
        internal const uint SwpFrameChanged = 0x0020;
        internal const int SwHide = 0;
        internal const int SwShowNoActivate = 4;
        internal const uint GwHwndPrev = 3;
        internal const uint WmNcLeftButtonDown = 0x00A1;
        internal const int HtCaption = 2;
        internal const int HtLeft = 10;
        internal const int HtRight = 11;
        internal const int HtTop = 12;
        internal const int HtTopLeft = 13;
        internal const int HtTopRight = 14;
        internal const int HtBottom = 15;
        internal const int HtBottomLeft = 16;
        internal const int HtBottomRight = 17;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { internal int Left; internal int Top; internal int Right; internal int Bottom; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint FindWindow(string? className, string? windowName);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint SetParent(nint child, nint newParent);

        [DllImport("user32.dll")]
        internal static extern nint GetWindow(nint hwnd, uint command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint GetWindowLongPtr(nint hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(nint hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(nint hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        internal static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    }
}
