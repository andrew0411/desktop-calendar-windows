using System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopCalendar.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _appIcon;
    private readonly Forms.ToolStripMenuItem _editLayoutItem;
    private readonly Forms.ToolStripMenuItem _autoStartItem;

    public TrayIconService(
        Action today,
        Action toggleLayout,
        Action settings,
        Action reattach,
        Action<bool> autoStart,
        Action exit)
    {
        _editLayoutItem = new Forms.ToolStripMenuItem("배치 편집", null, (_, _) => toggleLayout());
        _autoStartItem = new Forms.ToolStripMenuItem("Windows 시작 시 실행");
        _autoStartItem.Click += (_, _) => autoStart(!_autoStartItem.Checked);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("오늘로 이동", null, (_, _) => today());
        menu.Items.Add(_editLayoutItem);
        menu.Items.Add("설정", null, (_, _) => settings());
        menu.Items.Add("바탕화면 다시 연결", null, (_, _) => reattach());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => exit());

        var executablePath = Environment.ProcessPath;
        _appIcon = executablePath is null
            ? (Icon)SystemIcons.Application.Clone()
            : (Icon)(Icon.ExtractAssociatedIcon(executablePath)?.Clone() ?? SystemIcons.Application.Clone());
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "바탕화면 캘린더",
            Icon = _appIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => settings();
    }

    public void SetLayoutEditing(bool editing)
    {
        _editLayoutItem.Checked = editing;
        _editLayoutItem.Text = editing ? "배치 편집 완료" : "배치 편집";
    }

    public void SetAutoStart(bool enabled) => _autoStartItem.Checked = enabled;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appIcon.Dispose();
    }
}
