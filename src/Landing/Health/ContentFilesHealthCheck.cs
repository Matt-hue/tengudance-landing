using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

public sealed class ContentFilesHealthCheck(IWebHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stylesheet = environment.WebRootFileProvider.GetFileInfo("site.css");
        return stylesheet.Exists && stylesheet.Length > 0
            ? Diagnostic.Pass()
            : Diagnostic.Fail("The site's stylesheet is missing.", "The content files were not included when the image was built.");
    }
}
