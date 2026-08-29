using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPoint = System.Windows.Point;

namespace DesktopCalendar.App.Services;

public static class UiThemeService
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public static string CurrentMode { get; private set; } = Dark;

    public static string Normalize(string? mode) =>
        string.Equals(mode, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;

    public static void Apply(string? mode)
    {
        if (WpfApplication.Current is null)
            return;

        CurrentMode = Normalize(mode);
        var light = CurrentMode == Light;
        var resources = WpfApplication.Current.Resources;
        resources["UiWindowBackgroundBrush"] = Gradient(
            light ? "#FFF9FAFD" : "#FF0C1120",
            light ? "#FFEDF1F8" : "#FF171E31");
        resources["UiSurfaceBrush"] = Brush(light ? "#FBFFFFFF" : "#EB161D2D");
        resources["UiSurfaceElevatedBrush"] = Brush(light ? "#FFFFFFFF" : "#FF20293D");
        resources["UiSurfaceHoverBrush"] = Brush(light ? "#FFF0F3F9" : "#FF2A354C");
        resources["UiSurfaceSubtleBrush"] = Brush(light ? "#BFF5F7FB" : "#A61D2639");
        resources["UiInputBackgroundBrush"] = Brush(light ? "#FFFFFFFF" : "#FF1A2234");
        resources["UiBorderBrush"] = Brush(light ? "#FFDCE3EE" : "#2EFFFFFF");
        resources["UiBorderStrongBrush"] = Brush(light ? "#FFB9C5D6" : "#667D8DA8");
        resources["UiTextPrimaryBrush"] = Brush(light ? "#FF182033" : "#FFF7F8FC");
        resources["UiTextSecondaryBrush"] = Brush(light ? "#FF526075" : "#FFCAD2E0");
        resources["UiTextMutedBrush"] = Brush(light ? "#FF7B8799" : "#FF929DB0");
        resources["UiAccentBrush"] = Brush(light ? "#FF5A6FE5" : "#FF7C8CF8");
        resources["UiAccentHoverBrush"] = Brush(light ? "#FF465BCB" : "#FF95A1FF");
        resources["UiAccentSoftBrush"] = Brush(light ? "#245A6FE5" : "#307C8CF8");
        resources["UiAccentFaintBrush"] = Brush(light ? "#145A6FE5" : "#187C8CF8");
        resources["UiOnAccentBrush"] = Brush("#FFFFFFFF");
        resources["UiDangerBrush"] = Brush(light ? "#FFC73F55" : "#FFFF6B78");
        resources["UiDangerSoftBrush"] = Brush(light ? "#1CC73F55" : "#2BFF6B78");
        resources["UiSuccessBrush"] = Brush(light ? "#FF238A5B" : "#FF6BD6A2");
        resources["UiWeatherHighBrush"] = Brush(light ? "#FFC54A25" : "#FFFFA06B");
        resources["UiWeatherLowBrush"] = Brush(light ? "#FF276FB5" : "#FF78B7FF");
        resources["UiFooterBrush"] = Brush(light ? "#FAF8FAFD" : "#F3111725");
        resources["UiShadowColor"] = (WpfColor)WpfColorConverter.ConvertFromString(light ? "#300D1B34" : "#8A000000");
        foreach (Window window in WpfApplication.Current.Windows)
            ApplyWindowChrome(window);
    }

    public static void RegisterWindow(Window window)
    {
        if (new WindowInteropHelper(window).Handle != nint.Zero)
            ApplyWindowChrome(window);
        else
            window.SourceInitialized += (_, _) => ApplyWindowChrome(window);
    }

    private static void ApplyWindowChrome(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
            return;
        var enabled = CurrentMode == Dark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(string start, string end)
    {
        var brush = new LinearGradientBrush(
            (WpfColor)WpfColorConverter.ConvertFromString(start),
            (WpfColor)WpfColorConverter.ConvertFromString(end),
            new WpfPoint(0, 0),
            new WpfPoint(1, 1));
        brush.Freeze();
        return brush;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);
}
