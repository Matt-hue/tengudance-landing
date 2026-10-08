using Landing.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Services;

public sealed class DiagnosticReportService(HealthCheckService healthCheckService)
{
    public async Task<DiagnosticReport> GetReportAsync(CancellationToken cancellationToken)
    {
        var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken);
        var checks = healthReport.Entries.Select(entry => ToCheck(entry.Key, entry.Value)).ToArray();
        return new DiagnosticReport(DateTimeOffset.UtcNow, checks.All(check => check.Status != "failed"), checks);
    }

    private static DiagnosticCheck ToCheck(string name, HealthReportEntry entry)
    {
        if (entry.Data.ContainsKey(Diagnostic.SkippedKey))
        {
            return new(name, "skipped", entry.Description, null);
        }

        if (entry.Status == HealthStatus.Healthy)
        {
            return new(name, "passed", null, null);
        }

        // When a check throws, its description is the exception message. The health check
        // service logs the exception; the public report only says that the check broke.
        if (entry.Exception is not null)
        {
            return new(name, "failed", "The check could not be completed.", "An unexpected error; the application logs have the details.");
        }

        return new(name, "failed", entry.Description, entry.Data.GetValueOrDefault(Diagnostic.LikelyCauseKey) as string);
    }
}

public sealed record DiagnosticCheck(string Name, string Status, string? Message, string? LikelyCause);

public sealed record DiagnosticReport(DateTimeOffset CheckedAt, bool Healthy, IReadOnlyList<DiagnosticCheck> Checks);
