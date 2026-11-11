// using Worker.Enrichment;
// using Worker.Escalation;
// using Worker.Osv;
// using Worker.Scanning;

// namespace Sims.Worker.Demo;

// // Ausschließlich Demo-Antworten. Dieses Projekt wird vom normalen Worker NICHT referenziert.
// internal sealed class DemoSources : IOsvClient, IKevClient, IEpssClient
// {
//     public int Calls { get; private set; }
//     public Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(IReadOnlyList<OsvComponent> components, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         Calls++;
//         if (components.Any(c => c.Purl.Contains("Demo.Failure", StringComparison.Ordinal)))
//             throw new OsvUnavailableException("Absichtlich simulierter Ausfall, keine echte OSV-Anfrage.");
//         return Task.FromResult<IReadOnlyList<OsvFinding>>(components.Select(c => new OsvFinding(c.Id,
//             "DEMO-ADVISORY-001", "CVE-2099-10001", OsvSeverity.High, "Erfundener Demo-Fund")).ToArray());
//     }
//     public Task<HashSet<string>> GetKnownExploitedAsync(CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         return Task.FromResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
//     }
//     public Task<Dictionary<string, double>> GetScoresAsync(IReadOnlyList<string> cves, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         return Task.FromResult(cves.Distinct(StringComparer.OrdinalIgnoreCase)
//             .ToDictionary(c => c, _ => 0.2, StringComparer.OrdinalIgnoreCase));
//     }
// }

// internal sealed record DemoTicketBatch(IReadOnlyList<DemoTicket> Items) : IPreparedScanTickets
// {
//     public int Count => Items.Count;
// }

// internal sealed class DemoRules : IScanVerdictCalculator, IScanTicketPlanner, IEscalationPolicy
// {
//     // Feste Testantwort, KEINE Implementierung von B4-Grenzwerten.
//     public ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings) => ScanVerdict.GoWithConditions;
//     public Task<IPreparedScanTickets> PrepareAsync(PendingScan scan, IReadOnlyList<EnrichedFinding> findings,
//         DateTimeOffset now, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         // Ein vorbereitetes Beispielticket. C3-Deduplizierung wird hier NICHT nachgebaut.
//         return Task.FromResult<IPreparedScanTickets>(new DemoTicketBatch([new DemoTicket(now.AddSeconds(36))]));
//     }
//     // Vorgegebene Testfolge, KEINE vollständige C4-Policy.
//     public EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now) => ticket.EscalationLevel switch
//     {
//         1 => new(2, "Lead", now.AddSeconds(36)),
//         2 => new(3, "Admin", null),
//         _ => null
//     };
// }

// internal sealed class DemoClock : TimeProvider
// {
//     private DateTimeOffset now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
//     public override DateTimeOffset GetUtcNow() => now;
//     public void Advance(TimeSpan duration) => now += duration;
// }
