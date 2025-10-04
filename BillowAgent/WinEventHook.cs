using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BillowAgent;

public class WinEventHook : IDisposable
{
    private readonly Storage _storage;
    private readonly IdleMonitor _idle;
    private readonly Sessionizer _sessionizer;
    private IntPtr _hook;
    private WinEventDelegate? _proc;
    public bool Paused { get; set; }

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    public WinEventHook(Storage storage, IdleMonitor idle, Sessionizer sessionizer)
    { _storage = storage; _idle = idle; _sessionizer = sessionizer; }

    public void Start()
    {
        _proc = Callback;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
    }

    private void Callback(IntPtr hWinEventHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint idThread, uint time)
    {
        if (Paused) return;
        if (hwnd == IntPtr.Zero) return;

        var title = Native.GetWindowText(hwnd);
        var pid = Native.GetPidFromHwnd(hwnd);
        var exe = Native.GetProcessName(pid);
        var now = DateTime.UtcNow;

        _storage.RecordForegroundEvent(now, exe, title, hwnd);
        _sessionizer.OnForegroundChange(now, exe, title);
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private static class Native
    {
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static string GetWindowText(IntPtr hwnd)
        {
            int length = GetWindowTextLength(hwnd);
            var sb = new System.Text.StringBuilder(length + 1);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        public static int GetPidFromHwnd(IntPtr hwnd)
        { GetWindowThreadProcessId(hwnd, out uint pid); return (int)pid; }
        public static string GetProcessName(int pid)
        { try { return Process.GetProcessById(pid).ProcessName + ".exe"; } catch { return "unknown.exe"; } }
    }
}