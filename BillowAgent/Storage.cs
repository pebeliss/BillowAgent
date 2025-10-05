using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace BillowAgent;

public class Storage
{
    private readonly string _dbPath;
    public string DbDirectory => Path.GetDirectoryName(_dbPath)!;
    
    public Storage(string dbPath) { _dbPath = dbPath; }

    private IDbConnection Open()
    {
        Directory.CreateDirectory(DbDirectory);
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    public void Initialize()
    {
        using var db = Open();
        db.Execute(@"CREATE TABLE IF NOT EXISTS raw_events (
            id INTEGER PRIMARY KEY,
            ts_start TEXT NOT NULL,
            ts_end TEXT NOT NULL,
            exe TEXT NOT NULL,
            window_title TEXT,
            hwnd TEXT,
            was_idle INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS browser_events (
            id INTEGER PRIMARY KEY,
            ts_start TEXT NOT NULL,
            ts_end TEXT NOT NULL,
            browser TEXT NOT NULL,
            domain TEXT,
            title TEXT
        );
        CREATE TABLE IF NOT EXISTS rules (
            id INTEGER PRIMARY KEY,
            match_type TEXT NOT NULL, -- exe|domain|title_contains
            pattern TEXT NOT NULL,
            category TEXT,
            client TEXT,
            billable INTEGER
        );
        CREATE TABLE IF NOT EXISTS sessions (
            id INTEGER PRIMARY KEY,
            ts_start TEXT NOT NULL,
            ts_end TEXT NOT NULL,
            exe TEXT NOT NULL,
            primary_resource TEXT,
            category TEXT,
            client TEXT,
            billable INTEGER,
            confidence REAL
        );
        CREATE TABLE IF NOT EXISTS meeting_windows (
            id INTEGER PRIMARY KEY,
            start_utc TEXT NOT NULL,
            end_utc TEXT NOT NULL,
            provider TEXT NOT NULL
        );
        ");

        // Seed rules if empty
        var count = db.ExecuteScalar<int>("SELECT COUNT(1) FROM rules");
        if (count == 0)
        {
            db.Execute(@"INSERT INTO rules (match_type, pattern, category, client, billable) VALUES
                ('exe','ms-teams.exe','Teams Chat/Browsing',NULL,NULL),
                ('exe','Teams.exe','Teams Chat/Browsing',NULL,NULL),
                ('exe','OUTLOOK.EXE','Email',NULL,NULL),
                ('exe','EXCEL.EXE','Spreadsheet',NULL,1),
                ('exe','WINWORD.EXE','Document Editing',NULL,1),
                ('exe','POWERPNT.EXE','Slides',NULL,1),


                ('domain','atlassian.net','Jira/Confluence',NULL,1),
                ('domain','jira','Jira/Confluence',NULL,1),
                ('domain','confluence','Jira/Confluence',NULL,1),


                ('title_contains','Client A','', 'Client A', 1),
                ('title_contains','ACME','', 'Client A', 1)
            ;");
        }
    }

    // Raw capture API (very simplified): end previous segment and start new one
    public void RecordForegroundEvent(DateTime tsUtc, string exe, string title, IntPtr hwnd)
    {
        using var db = Open();
        // End last open raw_event
        db.Execute(@"UPDATE raw_events SET ts_end=@ts WHERE id = (SELECT id FROM raw_events ORDER BY id DESC LIMIT 1);",
            new { ts = tsUtc.ToString("o") });
        // Start new
        db.Execute(@"INSERT INTO raw_events (ts_start, ts_end, exe, window_title, hwnd, was_idle) VALUES (@s, @e, @exe, @title, @hwnd, 0);",
            new { s = tsUtc.ToString("o"), e = tsUtc.ToString("o"), exe, title, hwnd = hwnd.ToString() });
    }

    public void MarkIdleChange(DateTime tsUtc, bool idle)
    {
        using var db = Open();
        // Close current block
        db.Execute(@"UPDATE raw_events SET ts_end=@ts WHERE id = (SELECT id FROM raw_events ORDER BY id DESC LIMIT 1);");
        // Start an idle marker row
        db.Execute(@"INSERT INTO raw_events (ts_start, ts_end, exe, window_title, hwnd, was_idle) VALUES (@s, @e, @exe, @title, @hwnd, @idle);",
            new { s = tsUtc.ToString("o"), e = tsUtc.ToString("o"), exe = idle ? "IDLE" : "ACTIVE", title = idle ? "Idle" : "Active", hwnd = "0", idle = idle ? 1 : 0 });
    }

    public void RecordBrowserEvent(DateTime tsUtc, string url, string title)
    {
        var domain = ExtractDomain(url);
        var browser = "browser"; // optionally detect chrome/edge via foreground exe
        using var db = Open();
        db.Execute(@"UPDATE browser_events SET ts_end=@ts WHERE id = (SELECT id FROM browser_events ORDER BY id DESC LIMIT 1);");
        db.Execute(@"INSERT INTO browser_events (ts_start, ts_end, browser, domain, title) VALUES (@s,@e,@b,@d,@t);",
            new { s = tsUtc.ToString("o"), e = tsUtc.ToString("o"), b = browser, d = domain, t = title });
    }

    public void UpsertMeetingWindows(List<MeetingWindow> windows)
    {
        using var db = Open();
        db.Execute("DELETE FROM meeting_windows");
        foreach (var w in windows)
            db.Execute("INSERT INTO meeting_windows (start_utc,end_utc,provider) VALUES (@s,@e,@p)", new { s = w.StartUtc.ToString("o"), e = w.EndUtc.ToString("o"), p = w.Provider });
    }

    public IEnumerable<(string matchType, string pattern, string category, string? client, int? billable)> GetRules()
    {
        using var db = Open();
        return db.Query<(string,string,string,string?,int?)>("SELECT match_type, pattern, category, client, billable FROM rules");
    }

    private static string ExtractDomain(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u))
        {
            var h = u.Host;
            return string.Join('.', h.Split('.').TakeLast(3)); // coarse e.g., sub.domain.tld
        }
        return "unknown";
    }
}