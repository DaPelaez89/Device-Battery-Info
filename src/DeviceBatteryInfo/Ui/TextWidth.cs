namespace DeviceBatteryInfo.Ui;

// Text is laid out on the viewing device, so widths are estimated: em widths of SF Pro Semibold,
// erring wide. Only the list percentage needs it; captions are fitted by the reader (UiFirstFit).
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
}
