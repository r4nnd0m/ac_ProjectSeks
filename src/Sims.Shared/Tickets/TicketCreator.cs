using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Sims.Shared.Escalation;

namespace Sims.Shared.Tickets;

public class TicketCreator(EscalationPolicy policy)
{
    private const string ScannerName = "SIMS Scanner";

    public IReadOnlyList<Ticket> CreateFor(
        Project project,
        IEnumerable<Finding> findings,
        IEnumerable<Ticket> existingTickets,
        DateTimeOffset now)
    {
        HashSet<string> knownKeys = existingTickets
            .Where(t => t.Status != TicketStatus.Closed)
            .Select(t => KeyFor(t.ProjectId, t.Cve, t.System))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Ticket> created = [];

        foreach (Finding finding in findings)
        {
            if (!NeedsTicket(finding))
            {
                continue;
            }

            string vulnerability = finding.Cve ?? finding.VulnId;
            string key = KeyFor(project.Id, vulnerability, finding.Component.Name);

            if (!knownKeys.Add(key))
            {
                continue;
            }

            created.Add(BuildTicket(project, finding, vulnerability, now));
        }

        return created;
    }

    private static bool NeedsTicket(Finding finding)
    {
        return finding.IsKev || finding.Severity >= Severity.Medium;
    }

    private static string KeyFor(Guid? projectId, string? vulnerability, string? system)
    {
        return $"{projectId}|{vulnerability}|{system}";
    }

    private Ticket BuildTicket(Project project, Finding finding, string vulnerability, DateTimeOffset now)
    {
        EscalationStart start = policy.StartFor(finding.Severity, finding.IsKev, now);

        string title = $"{vulnerability} in {finding.Component.Name} {finding.Component.Version}";
        if (title.Length > Ticket.MaxTitleLength)
        {
            int end = Ticket.MaxTitleLength - 1;
            if (char.IsHighSurrogate(title[end - 1]))
                end--;
            title = title[..end] + "…";
        }

        NewTicket data = new NewTicket(
            project.TeamId,
            title,
            finding.Summary ?? "",
            finding.Severity,
            Guid.Empty,
            ScannerName,
            ProjectId: project.Id,
            FindingId: finding.Id,
            Cve: vulnerability,
            System: finding.Component.Name,
            EscalationLevel: start.Level,
            AssignedRole: start.AssignedRole,
            DueAt: start.DueAt);

        return new Ticket(data, now);
    }
}