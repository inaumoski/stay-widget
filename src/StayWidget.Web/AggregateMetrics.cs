using Microsoft.EntityFrameworkCore;
using StayWidget.Persistence;
namespace StayWidget.Web;

public sealed class AggregateMetrics(IConfiguration config, IHttpContextAccessor context, TimeProvider clock)
{
    public static readonly string[] Events = ["page-home","page-pricing","page-demo","page-install","page-limitations","widget-view","hosted-view","request-submitted","installation","attribution-product","attribution-portfolio"];
    private readonly object gate = new();
    private readonly Dictionary<(DateOnly,string),long> counts = new();
    public void Record(string value)
    {
        if (!config.GetValue("StayWidget:AggregateMetrics", false) || !Events.Contains(value)) return;
        var c = context.HttpContext;
        if (c == null || c.Request.Headers["DNT"] == "1" || c.Request.Headers["Sec-GPC"] == "1") return;
        var ua = c.Request.Headers.UserAgent.ToString();
        if (ua.Length == 0 || ua.Contains("bot",StringComparison.OrdinalIgnoreCase) || ua.Contains("crawler",StringComparison.OrdinalIgnoreCase) || c.Request.Headers["Purpose"] == "prefetch") return;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        lock(gate)
        {
            // At most two days of fixed event counters; no URL, tenant, token, IP or user identifiers.
            foreach (var k in counts.Keys.Where(k => k.Item1 < day.AddDays(-1)).ToArray()) counts.Remove(k);
            var key = (day,value); counts[key] = Math.Min(1_000_000,counts.GetValueOrDefault(key)+1);
        }
    }
    public async Task Flush(StayDb db, CancellationToken token = default)
    {
        Dictionary<(DateOnly,string),long> batch;
        lock(gate) { batch = new(counts); counts.Clear(); }
        // Best effort; failed flush/crash loses counts rather than delaying operational actions.
        foreach (var (key,count) in batch)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Metrics\" (\"Day\",\"Event\",\"Count\") VALUES ({key.Item1},{key.Item2},{count}) ON CONFLICT (\"Day\",\"Event\") DO UPDATE SET \"Count\"=LEAST(1000000000,\"Metrics\".\"Count\"+EXCLUDED.\"Count\")", token);
        var cutoff = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-90);
        await db.Metrics.Where(x => x.Day < cutoff).ExecuteDeleteAsync(token);
    }
}
public sealed class ProductMaintenanceWorker(IServiceScopeFactory scopes, AggregateMetrics metrics, ILogger<ProductMaintenanceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while(await timer.WaitForNextTickAsync(token))
            {
                try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<FeedbackService>().Cleanup(token); await metrics.Flush(scope.ServiceProvider.GetRequiredService<StayDb>(),token); }
                catch(OperationCanceledException) when(token.IsCancellationRequested) { break; }
                catch(Exception e) { log.LogWarning("Product maintenance failed ({FailureType}); retry on next tick.",e.GetType().Name); }
            }
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { }
    }
}
