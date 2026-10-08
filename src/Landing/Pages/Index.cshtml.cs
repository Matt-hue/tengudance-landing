using Landing.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Landing.Pages;

public sealed class IndexModel(DiagnosticReportService reports, IConfiguration configuration) : PageModel
{
    public DiagnosticReport Report { get; private set; } = null!;
    public string Version { get; private set; } = AppVersion.Unknown;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Report = await reports.GetReportAsync(cancellationToken);
        Version = AppVersion.From(configuration);
    }
}
