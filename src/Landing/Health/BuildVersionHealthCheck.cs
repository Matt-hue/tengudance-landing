using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class BuildVersionHealthCheck(IConfiguration configuration, ILogger<BuildVersionHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var version = configuration["APP_VERSION"];
        if (!string.IsNullOrWhiteSpace(version) && version != "unknown")
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }

        logger.LogWarning("Build version is unavailable; set APP_VERSION when building the image.");
        return Task.FromResult(HealthCheckResult.Unhealthy());
    }
}
