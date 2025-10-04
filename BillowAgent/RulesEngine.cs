using System;
using System.Linq;

namespace BillowAgent;

public class RulesEngine
{
    private readonly Storage _storage;
    public RulesEngine(Storage storage) { _storage = storage; }
    
    public (string category, string? client, bool? billable) Classify(string exe, string? title, string? domain, bool teamsMeeting)
    {
        if (teamsMeeting && (exe.Equals("ms-teams.exe", StringComparison.OrdinalIgnoreCase) || exe.Equals("Teams.exe", StringComparison.OrdinalIgnoreCase)))
            return ("Teams Meeting", null, true);

        foreach (var r in _storage.GetRules())
        {
            bool match = r.matchType switch
            {
                "exe" => exe.Equals(r.pattern, StringComparison.OrdinalIgnoreCase),
                "domain" => (domain ?? "").Contains(r.pattern, StringComparison.OrdinalIgnoreCase),
                "title_contains" => (title ?? "").Contains(r.pattern, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            if (match)
            {
                var cat = string.IsNullOrWhiteSpace(r.category) ? InferDefaultCategory(exe, domain) : r.category;
                bool? bill = r.billable is null ? null : r.billable == 1;
                return (cat, r.client, bill);
            }
        }

        return (InferDefaultCategory(exe, domain), null, null);
    }

    private static string InferDefaultCategory(string exe, string? domain)
    {
        return exe.ToUpperInvariant() switch
        {
            "OUTLOOK.EXE" => "Email",
            "EXCEL.EXE" => "Spreadsheet",
            "WINWORD.EXE" => "Document Editing",
            "POWERPNT.EXE" => "Slides",
            "MS-TEAMS.EXE" or "TEAMS.EXE" => "Teams Chat/Browsing",
            _ => domain != null ? "Browser-Research" : "Other"
        };
    }
}