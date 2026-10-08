using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Landing.Tests;

public sealed class HomePageTests
{
    // An address on a Docker network, where the Caddy container connects from in production.
    private const string ProxyAddress = "172.18.0.2";

    [Fact]
    public async Task Home_page_renders_content_without_caching()
    {
        using var client = new LandingFactory().CreateClient();

        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal("no-store", home.Headers.CacheControl?.ToString());
        Assert.Contains("tengudance", html);
        Assert.Contains("Matt's home on the web", html);
        Assert.Contains("href=\"https://github.com/matt-hue\"", html);
        Assert.Contains("Build test-sha", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task Healthz_returns_ok()
    {
        using var client = new LandingFactory().CreateClient();

        var health = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory]
    [InlineData("test-sha", "test-sha")]
    [InlineData(null, "unknown")]
    [InlineData(" ", "unknown")]
    public async Task Version_returns_build_sha_or_unknown(string? configured, string expected)
    {
        using var client = new LandingFactory(version: configured).CreateClient();

        Assert.Equal(expected, await client.GetStringAsync("/version"));
    }

    [Fact]
    public async Task Failing_checks_are_listed_without_affecting_home_page_or_liveness()
    {
        // A direct request with no proxy headers, like the local container test.
        using var client = new LandingFactory().CreateClient();

        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var status = await client.GetAsync("/status");
        var health = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Some systems need attention", html);
        Assert.Contains("The request did not arrive over HTTPS through the proxy.", html);
        Assert.Contains("The request was not addressed to the expected site name.", html);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Equal(["Secure connection", "Expected host"], await FailedChecks(status));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task All_checks_pass_behind_the_proxy()
    {
        using var client = new LandingFactory().CreateClient();
        AddProxyHeaders(client);

        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var status = await client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("All systems working", html);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var report = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.True(report.RootElement.GetProperty("healthy").GetBoolean());
        Assert.All(report.RootElement.GetProperty("checks").EnumerateArray(),
            check => Assert.Equal("passed", check.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Forwarded_headers_from_outside_private_networks_are_ignored()
    {
        using var client = new LandingFactory(remoteAddress: "203.0.113.7").CreateClient();
        AddProxyHeaders(client);

        var status = await client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Equal(["Secure connection", "Expected host"], await FailedChecks(status));
    }

    [Fact]
    public async Task Host_check_is_skipped_when_expected_host_is_unset()
    {
        using var client = new LandingFactory(expectedHost: null).CreateClient();
        AddProxyHeaders(client);

        var status = await client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var report = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        var hostCheck = report.RootElement.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "Expected host");
        Assert.Equal("skipped", hostCheck.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_check_that_throws_does_not_leak_its_error()
    {
        using var factory = new LandingFactory(configureServices: services => services.AddHealthChecks()
            .AddCheck("Broken", () => throw new InvalidOperationException("secret at /app/internal")));
        using var client = factory.CreateClient();
        AddProxyHeaders(client);

        var home = await client.GetAsync("/");
        var status = await client.GetAsync("/status");
        var health = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.DoesNotContain("secret", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.DoesNotContain("secret", await status.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static void AddProxyHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "tengudance.com");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.20");
    }

    private static async Task<string[]> FailedChecks(HttpResponseMessage status)
    {
        using var report = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.False(report.RootElement.GetProperty("healthy").GetBoolean());
        return report.RootElement.GetProperty("checks").EnumerateArray()
            .Where(check => check.GetProperty("status").GetString() == "failed")
            .Select(check => check.GetProperty("name").GetString()!)
            .ToArray();
    }

    private sealed class LandingFactory(
        string? expectedHost = "tengudance.com",
        string? version = "test-sha",
        string remoteAddress = ProxyAddress,
        Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EXPECTED_HOST"] = expectedHost,
                    ["APP_VERSION"] = version
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(IPAddress.Parse(remoteAddress)));
                configureServices?.Invoke(services);
            });
        }
    }

    // The test server has no real connection, so give requests the address they would come from.
    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
