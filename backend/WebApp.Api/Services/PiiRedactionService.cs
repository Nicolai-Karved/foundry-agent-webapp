using System.Text.RegularExpressions;

namespace WebApp.Api.Services;

public sealed class PiiRedactionService
{
    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex PhoneRegex = new(
        @"(?<!\w)(?:\+?\d[\d\s\-().]{7,}\d)(?!\w)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public bool IsEnabled { get; }

    public PiiRedactionService(IConfiguration configuration)
    {
        IsEnabled = configuration.GetValue<bool>("Privacy:EnablePiiRedaction");
    }

    public string? Redact(string? value)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = EmailRegex.Replace(value, "[REDACTED_EMAIL]");
        redacted = PhoneRegex.Replace(redacted, "[REDACTED_PHONE]");

        return redacted;
    }
}
