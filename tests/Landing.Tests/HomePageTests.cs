using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Landing.Tests;

public sealed class HomePageTests
{
    [Fact]
    public async Task Diagnostics_can_fail_without_affecting_home_page_or_liveness()
    {
        using var factory = new LandingFactory();
        using var client = factory.CreateClient();

        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var status = await client.GetAsync("/status");
        var health = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Some systems need attention", html);
        Assert.Contains("The request was not identified as HTTPS.", html);
        Assert.Equal("no-store", home.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Forwarded_https_and_expected_host_allow_all_checks_to_pass()
    {
        using var factory = new LandingFactory(expectedHost: "tengudance.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "tengudance.com");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var status = await client.GetAsync("/status");
        var health = await client.GetAsync("/healthz");
        var version = await client.GetStringAsync("/version");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("All systems working", html);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("test-sha", version);
        Assert.Equal("no-store", status.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Version_endpoint_falls_back_to_unknown()
    {
        using var factory = new LandingFactory(version: null);
        using var client = factory.CreateClient();

        Assert.Equal("unknown", await client.GetStringAsync("/version"));
    }

    private sealed class LandingFactory(string? expectedHost = null, string? version = "test-sha") : WebApplicationFactory<Program>
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
        }
    }
}
