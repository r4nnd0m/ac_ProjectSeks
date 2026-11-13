namespace NewsApi.Options;

public class NewsOptions
{
    public const string SectionName = "News";

    // Nach Ablauf lädt die nächste Anfrage die Feeds neu.
    public int RefreshMinutes { get; set; } = 30;
}
