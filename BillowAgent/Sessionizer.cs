using System;
using System.Linq;

namespace BillowAgent;

public class Sessionizer
{
    private readonly Storage _storage;
    private readonly RulesEngine _rules;
    private readonly AppConfig _cfg;
    
    // crude in-memory current state
    private DateTime _currentStartUtc;
    private string _currentExe = "";
    private string? _currentTitle;

    public Sessionizer(Storage storage, RulesEngine rules, AppConfig cfg)
    { _storage = storage; _rules = rules; _cfg = cfg; }

    public void OnForegroundChange(DateTime nowUtc, string exe, string title)
    {
        if (_currentExe == "") { _currentExe = exe; _currentTitle = title; _currentStartUtc = nowUtc; return; }
    
        var dur = nowUtc - _currentStartUtc;
        if (dur.TotalSeconds >= _cfg.Rules.MinFocusSeconds)
        {
            // Determine domain if title hints come from browser_events (left as an exercise to join by time)
            string? domain = null; bool inTeamsMeeting = IsInTeamsMeeting(nowUtc);
            var (cat, client, bill) = _rules.Classify(_currentExe, _currentTitle, domain, inTeamsMeeting);

            SaveSession(_currentStartUtc, nowUtc, _currentExe, PrimaryResource(_currentExe, _currentTitle, domain), cat, client, bill, 0.8);
        }

        _currentExe = exe; _currentTitle = title; _currentStartUtc = nowUtc;
    }

    private static string PrimaryResource(string exe, string? title, string? domain)
    => exe.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase) && exe.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) ? (domain ?? "") : (title ?? "");

    private bool IsInTeamsMeeting(DateTime whenUtc)
    {
        // naive: read meeting_windows table to see overlap
        // you can cache in memory if you like
        // (left simple for clarity)
        return false; // TODO: implement a quick lookup
    }

    private void SaveSession(DateTime startUtc, DateTime endUtc, string exe, string resource, string category, string? client, bool? billable, double confidence)
    {
        // Insert a session row (merging logic can be added later)
        // For brevity, call into Storage with direct SQL or Dapper here if you want.
        using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_storage.GetType().GetField("_dbPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_storage)}");
        db.Open();
        var cmd = db.CreateCommand();
        cmd.CommandText = @"INSERT INTO sessions (ts_start, ts_end, exe, primary_resource, category, client, billable, confidence)
                            VALUES ($s,$e,$x,$r,$c,$cl,$b,$f);";
        cmd.Parameters.AddWithValue("$s", startUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$e", endUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$x", exe);
        cmd.Parameters.AddWithValue("$r", resource);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$cl", (object?)client ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$b", billable is null ? DBNull.Value : (object)((bool)billable ? 1 : 0));
        cmd.Parameters.AddWithValue("$f", confidence);
        cmd.ExecuteNonQuery();
    }
}