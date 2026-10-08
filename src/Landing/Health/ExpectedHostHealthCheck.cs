using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ExpectedHostHealthCheck(
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ExpectedHostHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var expectedHost = configuration["EXPECTED_HOST"];
        if (string.IsNullOrWhiteSpace(expectedHost))
        {
            return Task.FromResult(HealthCheckResult.Healthy(data: new Dictionary<string, object> { ["skipped"] = true }));
        }

        var actualHost = httpContextAccessor.HttpContext?.Request.Host.Host;
        if (string.Equals(actualHost, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }

        logger.LogWarning("Request host did not match EXPECTED_HOST.");
        return Task.FromResult(HealthCheckResult.Unhealthy());
    }
}
