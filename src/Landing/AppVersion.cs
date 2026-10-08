namespace Landing;

public static class AppVersion
{
    public const string Unknown = "unknown";

    // APP_VERSION is baked into the image from the commit SHA build argument.
    public static string From(IConfiguration configuration) =>
        configuration["APP_VERSION"] is { } version && !string.IsNullOrWhiteSpace(version) ? version.Trim() : Unknown;
}
