using System.Windows;
using System.Windows.Controls;

namespace DesktopCalendar.App.Controls;

public partial class PaletteColorPicker : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(string), typeof(PaletteColorPicker),
        new FrameworkPropertyMetadata("#FFFFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));
    private static readonly DependencyPropertyKey SelectedNamePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SelectedName), typeof(string), typeof(PaletteColorPicker), new PropertyMetadata("흰색"));
    public static readonly DependencyProperty SelectedNameProperty = SelectedNamePropertyKey.DependencyProperty;

    public PaletteColorPicker()
    {
        InitializeComponent();
        UpdateSelectedName();
    }

    public byte Alpha { get; set; } = 255;

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public string SelectedName => (string)GetValue(SelectedNameProperty);

    public IReadOnlyList<PaletteColor> Palette { get; } =
    [
        new("흰색", "#FFFFFFFF"), new("연회색", "#FFE2E7F0"), new("회색", "#FF8C96A8"), new("차콜", "#FF343B4A"), new("검정", "#FF12151C"), new("브라운", "#FF8D6652"),
        new("레드", "#FFE65F6C"), new("코랄", "#FFFF7B78"), new("오렌지", "#FFFF9F43"), new("골드", "#FFE7B84B"), new("옐로", "#FFF2D95C"), new("라임", "#FFA8CF5A"),
        new("그린", "#FF58B881"), new("민트", "#FF50C7B7"), new("청록", "#FF3DA9B8"), new("하늘", "#FF66B8E8"), new("블루", "#FF4F8EF7"), new("네이비", "#FF4C63B6"),
        new("라벤더", "#FF9B83E3"), new("퍼플", "#FF805AD5"), new("바이올렛", "#FFB36AD8"), new("마젠타", "#FFD85CA5"), new("핑크", "#FFF07EA8"), new("로즈", "#FFD96B85")
    ];

    public event RoutedPropertyChangedEventHandler<string>? SelectedColorChanged;

    private void PickerButton_Click(object sender, RoutedEventArgs e) => PalettePopup.IsOpen = true;

    private void PaletteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: PaletteColor color })
            return;
        var rgb = color.Hex.Length == 9 ? color.Hex[3..] : color.Hex.TrimStart('#');
        SelectedColor = $"#{Alpha:X2}{rgb}";
        PalettePopup.IsOpen = false;
    }

    private static void OnSelectedColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var picker = (PaletteColorPicker)dependencyObject;
        picker.UpdateSelectedName();
        picker.SelectedColorChanged?.Invoke(picker, new RoutedPropertyChangedEventArgs<string>((string?)args.OldValue ?? string.Empty, (string?)args.NewValue ?? string.Empty));
    }

    private void UpdateSelectedName()
    {
        var rgb = SelectedColor.Length == 9 ? SelectedColor[3..] : SelectedColor.TrimStart('#');
        SetValue(SelectedNamePropertyKey, Palette.FirstOrDefault(item => item.Hex.EndsWith(rgb, StringComparison.OrdinalIgnoreCase))?.Name ?? "현재 색상");
    }

    public sealed record PaletteColor(string Name, string Hex);
}
