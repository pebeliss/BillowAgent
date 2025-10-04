using System;

namespace BillowAgent;

public record ForegroundEvent(DateTime TsUtc, string Exe, string Title, IntPtr Hwnd);
public record BrowserEvent(DateTime TsUtc, string Browser, string Domain, string? Title);

public record Session(
    long Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string Exe,
    string PrimaryResource,
    string Category,
    string? Client,
    bool? Billable,
    double Confidence);


public record MeetingWindow(DateTime StartUtc, DateTime EndUtc, string Provider); // teamsForBusiness


public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public WebSocketConfig WebSocket { get; set; } = new();
    public RulesConfig Rules { get; set; } = new();
    public GraphConfig Graph { get; set; } = new();
    public void ResolveEnvVars()
    { Database.Path = Environment.ExpandEnvironmentVariables(Database.Path); }
}
public class DatabaseConfig { public string Path { get; set; } = "%APPDATA%/TimeAgent/timeagent.db"; }
public class WebSocketConfig { public string Prefix { get; set; } = "http://localhost:57451/ws/"; }
public class RulesConfig { public int MinFocusSeconds { get; set; } = 15; public int MergeGapSeconds { get; set; } = 120; public int IdleThresholdSeconds { get; set; } = 240; }
public class GraphConfig { public string TenantId { get; set; } = "common"; public string ClientId { get; set; } = ""; public string[] Scopes { get; set; } = new[] { "User.Read","Calendars.Read","Presence.Read" }; }