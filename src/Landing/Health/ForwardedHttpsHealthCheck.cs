using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ForwardedHttpsHealthCheck(IHttpContextAccessor httpContextAccessor) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        httpContextAccessor.HttpContext?.Request.IsHttps == true
            ? Diagnostic.Pass()
            : Diagnostic.Fail(
                "The request did not arrive over HTTPS through the proxy.",
                "The site is being reached directly, or the proxy's forwarded headers are not being trusted.");
}
