using System.Text.RegularExpressions;
using StudyAbroad.Application.Leads;

namespace StudyAbroad.Application.Admin.SlmSamples;

/// <summary>Nhận ra số điện thoại, e-mail, đường link trong văn bản. Không nhận ra được họ tên.</summary>
public static partial class SlmPersonalDataCheck
{
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)\bhttps?://\S+|\bwww\.\S+|\b[a-z0-9\-]+(?:\.[a-z0-9\-]+)*\.(?:com|net|org|vn|edu|io|me|ly|gl|ee)(?:/\S*)?(?![a-z0-9])")]
    private static partial Regex UrlRegex();

    public static bool HasContact(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && (EmailRegex().IsMatch(text) || LeadMessageContentFilter.ContainsPhoneNumber(text));

    public static bool HasContactOrLink(string? text) =>
        HasContact(text) || (!string.IsNullOrWhiteSpace(text) && UrlRegex().IsMatch(text));
}
