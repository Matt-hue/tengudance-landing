using Landing;
using Landing.Health;
using Landing.Services;
using Microsoft.AspNetCore.HttpOverrides;

// Docker's HEALTHCHECK runs this, because the runtime image has no curl or wget.
if (args is ["--healthcheck"])
{
    return await ProbeLivenessAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DiagnosticReportService>();
builder.Services.AddHealthChecks()
    .AddCheck<BuildVersionHealthCheck>("Build version")
    .AddCheck<ForwardedHttpsHealthCheck>("Secure connection")
    .AddCheck<ExpectedHostHealthCheck>("Expected host")
    .AddCheck<ContentFilesHealthCheck>("Site content");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    // Only loopback is trusted by default, but Caddy runs in another container and connects from
    // a Docker network address. The container publishes no ports, so only something on those
    // private networks can reach it and set these headers.
    foreach (var range in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(range));
    }
});

var app = builder.Build();

// Must come first so everything after it sees the scheme and host the browser used.
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();

// Liveness only. It deliberately ignores the diagnostic checks: a proxy or configuration problem
// should show on the status panel, not make Docker mark the container unhealthy and stop a deploy.
app.MapGet("/healthz", () => Results.Text("ok"));
app.MapGet("/version", (IConfiguration configuration) => Results.Text(AppVersion.From(configuration)));
app.MapGet("/status", async (DiagnosticReportService reports, CancellationToken cancellationToken) =>
{
    var report = await reports.GetReportAsync(cancellationToken);
    return Results.Json(report, statusCode: report.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapRazorPages();

await app.RunAsync();
return 0;

static async Task<int> ProbeLivenessAsync()
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await client.GetAsync("http://localhost:8080/healthz");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception)
    {
        return 1;
    }
}
