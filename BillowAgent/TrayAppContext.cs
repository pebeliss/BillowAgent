// BillowAgent/TrayAppContext.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BillowAgent;

public class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly Storage _storage;
    private readonly WinEventHook _hook;
    private readonly IdleMonitor _idle;
    private readonly BrowserServer _browser;
    private readonly GraphService _graph;
    private bool _paused;

    public TrayAppContext(Storage storage, WinEventHook hook, IdleMonitor idle, BrowserServer browser, GraphService graph)
    {
        _storage = storage; _hook = hook; _idle = idle; _browser = browser; _graph = graph;

        _tray = new NotifyIcon
        {
            Text = "BillowAgent",
            Visible = true,
            Icon = SystemIcons.Application
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Pause Tracking", null, (_,__) => TogglePause());
        menu.Items.Add("Sync Calendar Now", null, async (_,__) => await SyncCalendar());
        menu.Items.Add("Open DB Folder", null, (_,__) => Process.Start("explorer.exe", _storage.DbDirectory));
        menu.Items.Add("Exit", null, (_,__) => ExitThread());
        _tray.ContextMenuStrip = menu;

        try { _browser.Start(); }
        catch (Exception ex)
        {
            _tray.BalloonTipTitle = "Browser listener disabled";
            _tray.BalloonTipText = ex.Message;
            _tray.ShowBalloonTip(4000);
        }
        _hook.Start();
        _idle.Start();
        // Optional: kickoff background calendar sync
        _ = System.Threading.Tasks.Task.Run(SyncCalendar);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _hook.Paused = _paused;
        _idle.Paused = _paused;
        _tray.BalloonTipTitle = _paused ? "Tracking paused" : "Tracking resumed";
        _tray.ShowBalloonTip(1500);
        ((ToolStripMenuItem)_tray.ContextMenuStrip!.Items[0]).Text = _paused ? "Resume Tracking" : "Pause Tracking";
    }

    private async Task SyncCalendar()
    {
        try
        {
            var windows = await _graph.GetTodaysOnlineMeetingWindowsUtc();
            _storage.UpsertMeetingWindows(windows);
            _tray.BalloonTipTitle = $"Synced {windows.Count} meeting windows";
            _tray.ShowBalloonTip(1500);
        }
        catch (Exception ex)
        {
            _tray.BalloonTipTitle = "Graph sync failed";
            _tray.BalloonTipText = ex.Message;
            _tray.ShowBalloonTip(3000);
        }
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        base.ExitThreadCore();
    }
}