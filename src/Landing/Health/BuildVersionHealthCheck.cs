using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class BuildVersionHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        AppVersion.From(configuration) != AppVersion.Unknown
            ? Diagnostic.Pass()
            : Diagnostic.Fail("The build version is unknown.", "The image was built without the APP_VERSION build argument.");
}
