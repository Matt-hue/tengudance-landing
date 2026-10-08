using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Landing.Health;

// Results for the status panel. Each check writes its own plain-English text, which is shown
// publicly, so it must never include request values, environment values or exception details.
public static class Diagnostic
{
    public const string LikelyCauseKey = "likelyCause";
    public const string SkippedKey = "skipped";

    public static Task<HealthCheckResult> Pass() =>
        Task.FromResult(HealthCheckResult.Healthy());

    // Reported as healthy so a skipped check never fails the report.
    public static Task<HealthCheckResult> Skip(string reason) =>
        Task.FromResult(HealthCheckResult.Healthy(reason, new Dictionary<string, object> { [SkippedKey] = true }));

    public static Task<HealthCheckResult> Fail(string problem, string likelyCause) =>
        Task.FromResult(HealthCheckResult.Unhealthy(problem, data: new Dictionary<string, object> { [LikelyCauseKey] = likelyCause }));
}
