using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace BillowAgent;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // Load config
        var cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(cfgPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json)!;
        config.ResolveEnvVars();
        
        // Initialize storage (creates DB + tables + seed rules if needed)
        var storage = new Storage(config.Database.Path);
        storage.Initialize();
        
        // Compose services
        var rules = new RulesEngine(storage);
        var idle = new IdleMonitor(TimeSpan.FromSeconds(config.Rules.IdleThresholdSeconds));
        var browser = new BrowserServer(config.WebSocket.Prefix, storage);
        var sessionizer = new Sessionizer(storage, rules, config);
        var graph = new GraphService(config.Graph);
        
        // WinEvent hook -> records foreground changes
        var winHook = new WinEventHook(storage, idle, sessionizer);
        
        Application.Run(new TrayAppContext(storage, winHook, idle, browser, graph));
        
        // Clean up
        winHook.Dispose();
        browser.Dispose();
    }
}