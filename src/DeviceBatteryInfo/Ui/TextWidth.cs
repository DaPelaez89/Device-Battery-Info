using System.Globalization;
using System.Text;
using MacroDeck.Localization;

namespace DeviceBatteryInfo.Ui;

// Text is laid out on the viewing device, so widths are estimated: em widths of SF Pro Semibold,
// erring wide.
internal static class TextWidth
{
    private const string NarrowChars = " .,:;'!|iljtfrI-/()[]";
    private const string WideChars = "mwMW%@";

    private const double NarrowEm = 0.36;
    private const double RegularEm = 0.6;
    private const double CapitalEm = 0.7;
    private const double WideEm = 0.95;

    public static double Of(string text, double size) =>
        text.Sum(c =>
            NarrowChars.Contains(c) ? NarrowEm
            : WideChars.Contains(c) ? WideEm
            : char.IsUpper(c) || char.IsDigit(c) ? CapitalEm
            : RegularEm
        ) * size;

    // The viewer's culture is unknown, so the widest translation counts.
    public static double Of(LocalizedText text, double size) =>
        text.Localized is { } localized
            ? Strings.LocalizationCatalog.Cultures
                .Select(culture => Resolve(localized, culture))
                .OfType<string>()
                .Select(resolved => Of(resolved, size))
                .DefaultIfEmpty(0)
                .Max()
            : Of(text.Literal, size);

    private static string? Resolve(LocalizedString text, string culture)
    {
        if (!Strings.LocalizationCatalog.TryGetTemplate(culture, text.Key.Name, out var template))
        {
            return null;
        }

        var resolved = new StringBuilder(template);
        foreach (var (name, value) in text.Arguments)
        {
            resolved.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        return resolved.ToString();
    }
}
