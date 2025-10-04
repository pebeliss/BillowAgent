using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;

namespace BillowAgent;

public class GraphService
{
    private readonly GraphConfig _cfg;
    private GraphServiceClient? _client;

    public GraphService(GraphConfig cfg) { _cfg = cfg; }

    private GraphServiceClient Client
    {
        get
        {
            if (_client == null)
            {
                var opts = new InteractiveBrowserCredentialOptions
                {
                    TenantId = _cfg.TenantId,
                    ClientId = _cfg.ClientId,
                    RedirectUri = new Uri("http://localhost")
                };
                var cred = new InteractiveBrowserCredential(opts);
                _client = new GraphServiceClient(cred, _cfg.Scopes);
            }
            return _client;
        }
    }

    public async Task<List<MeetingWindow>> GetTodaysOnlineMeetingWindowsUtc()
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);
        var resp = await Client.Me.CalendarView.GetAsync(req =>
        {
            req.QueryParameters.StartDateTime = start.ToString("o");
            req.QueryParameters.EndDateTime = end.ToString("o");
            req.QueryParameters.Select = new[] { "subject", "start", "end", "isOnlineMeeting", "onlineMeetingProvider" };
        });


        var list = new List<MeetingWindow>();
        if (resp?.Value != null)
        {
            foreach (var ev in resp.Value)
            {
                if (ev.IsOnlineMeeting == true && ev.OnlineMeetingProvider?.ToString()?.Contains("teams", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var s = DateTime.Parse(ev.Start!.DateTime!).ToUniversalTime();
                    var e = DateTime.Parse(ev.End!.DateTime!).ToUniversalTime();
                    list.Add(new MeetingWindow(s, e, "teamsForBusiness"));
                }
            }
        }
        return list;
    }
}