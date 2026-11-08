namespace Worker;

/// <summary>Einstellungen für die regelmäßigen Worker-Durchläufe.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int PollIntervalSeconds { get; set; } = 30;
}
