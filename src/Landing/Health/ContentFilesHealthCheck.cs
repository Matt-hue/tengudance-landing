using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ContentFilesHealthCheck(
    IWebHostEnvironment environment,
    ILogger<ContentFilesHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = environment.WebRootFileProvider.GetFileInfo("site.css");
            if (file.Exists && file.Length > 0)
            {
                using var stream = file.CreateReadStream();
                if (stream.ReadByte() >= 0)
                {
                    return Task.FromResult(HealthCheckResult.Healthy());
                }
            }

            logger.LogWarning("Required site content file was missing, empty, or unreadable.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not read required site content file.");
        }

        return Task.FromResult(HealthCheckResult.Unhealthy());
    }
}
