using System;
using System.Runtime.InteropServices;
using System.Timers;

namespace BillowAgent;

public class IdleMonitor
{
    private readonly System.Timers.Timer _timer;
    private readonly TimeSpan _threshold;
    public bool IsIdle { get; private set; }
    public bool Paused { get; set; }

    public event Action<bool, DateTime>? OnIdleChanged; // (isIdle, atUtc)

    public IdleMonitor(TimeSpan threshold)
    {
        _threshold = threshold;
        _timer = new Timer(3000);
        _timer.Elapsed += (_,__) => Tick();
    }

    public void Start() => _timer.Start();

    private void Tick()
    {
        if (Paused) return;
        var idle = GetIdleTime() >= _threshold;
        if (idle != IsIdle)
        {
            IsIdle = idle;
            OnIdleChanged?.Invoke(IsIdle, DateTime.UtcNow);
        }
    }

    private static TimeSpan GetIdleTime()
    {
        LASTINPUTINFO lii = new() { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        uint tick = (uint)Environment.TickCount;
        return TimeSpan.FromMilliseconds(tick - lii.dwTime);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}