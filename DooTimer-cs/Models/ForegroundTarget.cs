namespace DooTimer.Models;

public sealed record ForegroundTarget(
    bool IsDouyin,
    string Source,
    string ProcessName,
    string WindowTitle
)
{
    public static ForegroundTarget Empty { get; } = new(false, "other", "", "");
}
