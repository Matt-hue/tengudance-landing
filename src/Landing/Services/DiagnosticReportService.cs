using Landing.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Services;

public sealed class DiagnosticReportService(HealthCheckService healthCheckService)
{
    public async Task<DiagnosticReport> GetReportAsync(CancellationToken cancellationToken)
    {
        var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken);
        var checks = healthReport.Entries.Select(entry =>
        {
            var description = Descriptions[entry.Key];
            var skipped = entry.Value.Data.TryGetValue("skipped", out var value) && value is true;
            var failed = entry.Value.Status != HealthStatus.Healthy;
            return new DiagnosticCheck(
                description.Name,
                skipped ? "skipped" : failed ? "failed" : "passed",
                failed ? description.Failure : skipped ? description.Skipped : description.Passed,
                failed ? description.Cause : null,
                skipped);
        }).ToArray();

        return new DiagnosticReport(DateTimeOffset.UtcNow, checks.All(check => check.Status != "failed"), checks);
    }

    private static readonly IReadOnlyDictionary<string, CheckDescription> Descriptions =
        new Dictionary<string, CheckDescription>
        {
            ["build-version"] = new("Build version", "The build version is unavailable.", "The image may have been built without its commit SHA."),
            ["forwarded-https"] = new("Secure connection", "The request was not identified as HTTPS.", "Check the proxy or forwarded-header configuration."),
            ["expected-host"] = new("Expected host", "The request host does not match the configured site host.", "Check the proxy host routing and EXPECTED_HOST setting."),
            ["content-files"] = new("Site content", "A required site content file could not be read.", "Check that the application content was included in the image.")
        };

    private sealed record CheckDescription(string Name, string Failure, string Cause)
    {
        public string Passed => "Working as expected.";
        public string Skipped => "Skipped because EXPECTED_HOST is not configured.";
    }
}

public sealed record DiagnosticCheck(string Name, string Status, string Message, string? LikelyCause, bool Skipped);

public sealed record DiagnosticReport(DateTimeOffset CheckedAt, bool Healthy, IReadOnlyList<DiagnosticCheck> Checks);
