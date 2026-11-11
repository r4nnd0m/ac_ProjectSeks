namespace NewsApi.Options;
public class NewsOptions
{
    public const string SectionName = "News";

    // wie lang der Feed bleibt
    public int RefreshMinutes { get; set; } = 30;
}
