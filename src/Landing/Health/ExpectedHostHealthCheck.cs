using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ExpectedHostHealthCheck(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var expectedHost = configuration["EXPECTED_HOST"];
        if (string.IsNullOrWhiteSpace(expectedHost))
        {
            return Diagnostic.Skip("Skipped because EXPECTED_HOST is not set.");
        }

        var actualHost = httpContextAccessor.HttpContext?.Request.Host.Host;
        return string.Equals(actualHost, expectedHost.Trim(), StringComparison.OrdinalIgnoreCase)
            ? Diagnostic.Pass()
            : Diagnostic.Fail(
                "The request was not addressed to the expected site name.",
                "The proxy is not forwarding the original host, or EXPECTED_HOST is set to the wrong name.");
    }
}
