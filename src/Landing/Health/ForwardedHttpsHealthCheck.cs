using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ForwardedHttpsHealthCheck(IHttpContextAccessor httpContextAccessor, ILogger<ForwardedHttpsHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request?.Scheme == "https")
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }

        logger.LogWarning("Request was not marked HTTPS after forwarded-header processing. Scheme: {Scheme}, RemoteAddress: {RemoteAddress}",
            request?.Scheme, request?.HttpContext.Connection.RemoteIpAddress);
        return Task.FromResult(HealthCheckResult.Unhealthy());
    }
}
