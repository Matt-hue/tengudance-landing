using System.Net.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Landing.Health;
using Landing.Services;

if (args is ["--healthcheck"])
{
    return await RunHealthcheckAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddCheck<BuildVersionHealthCheck>("build-version")
    .AddCheck<ForwardedHttpsHealthCheck>("forwarded-https")
    .AddCheck<ExpectedHostHealthCheck>("expected-host")
    .AddCheck<ContentFilesHealthCheck>("content-files");
builder.Services.AddSingleton<DiagnosticReportService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    // The app is reachable only through the proxy container on these private networks.
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.MapGet("/healthz", () => Results.Ok());
app.MapGet("/version", (IConfiguration configuration) =>
    Results.Text(configuration["APP_VERSION"] ?? "unknown", "text/plain"));
app.MapGet("/status", async (DiagnosticReportService reports, CancellationToken cancellationToken) =>
{
    var report = await reports.GetReportAsync(cancellationToken);
    return Results.Json(report, statusCode: report.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapRazorPages();

await app.RunAsync();
return 0;

static async Task<int> RunHealthcheckAsync()
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    try
    {
        using var response = await client.GetAsync("http://localhost:8080/healthz");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        return 1;
    }
    catch (TaskCanceledException)
    {
        return 1;
    }
}

public partial class Program { }
