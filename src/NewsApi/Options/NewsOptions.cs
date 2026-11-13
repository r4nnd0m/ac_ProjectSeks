namespace NewsApi.Options;

public class NewsOptions
{
    public const string SectionName = "News";

    // Wie lange die geladenen Meldungen im Cache bleiben.
    public int RefreshMinutes { get; set; } = 30;
}
