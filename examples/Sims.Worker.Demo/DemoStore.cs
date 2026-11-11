// using Worker.Escalation;
// using Worker.Osv;
// using Worker.Scanning;

// namespace Sims.Worker.Demo;

// // Dieser Speicher zeigt den Ablauf. Er ersetzt keine SQL-Transaktion und keine C1-Entity.
// internal sealed class DemoStore : IScanStore, IEscalationStore, IEscalationAudit
// {
//     private sealed class Row(PendingScan scan, string name, string status, OsvComponent component)
//     {
//         public PendingScan Scan { get; } = scan;
//         public string Name { get; } = name;
//         public string Status { get; set; } = status;
//         public OsvComponent Component { get; } = component;
//         public ScanCompletion? Result { get; set; }
//         public string? Error { get; set; }
//         public DateTimeOffset? FinishedAt { get; set; }
//     }
//     private readonly List<Row> rows;
//     private readonly List<EscalationAuditEntry> pendingAudit = [];
//     public List<DemoTicket> Tickets { get; } = [];
//     public List<EscalationAuditEntry> Audit { get; } = [];
//     public int SuccessCount => rows.Count(r => r.Status == "Scanned");
//     public int FailureCount => rows.Count(r => r.Status == "Failed");

//     public DemoStore(DateTimeOffset now)
//     {
//         rows = Enumerable.Range(1, 3).Select(i => new Row(
//             new PendingScan(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(i - 4)),
//             i == 1 ? "Normaler Demo-Auftrag" : i == 2 ? "Unterbrochener Demo-Auftrag" : "Demo-Quellenausfall",
//             i == 2 ? "Scanning" : "Pending",
//             new OsvComponent(Guid.NewGuid(), i == 3 ? "pkg:nuget/Demo.Failure@1.0.0" : "pkg:nuget/Demo.Component@1.0.0"))).ToList();
//     }
//     public Task<IReadOnlyList<PendingScan>> GetPendingAsync(int limit, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         return Task.FromResult<IReadOnlyList<PendingScan>>(rows.Where(r => r.Status == "Pending")
//             .OrderBy(r => r.Scan.CreatedAt).ThenBy(r => r.Scan.Id).Take(limit).Select(r => r.Scan).ToArray());
//     }
//     public Task<bool> TryStartAsync(Guid scanId, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         var row = rows.Single(r => r.Scan.Id == scanId);
//         if (row.Status != "Pending") return Task.FromResult(false);
//         row.Status = "Scanning";
//         row.Result = null;
//         row.Error = null;
//         row.FinishedAt = null;
//         return Task.FromResult(true);
//     }
//     public Task<IReadOnlyList<OsvComponent>> GetComponentsAsync(Guid scanId, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         return Task.FromResult<IReadOnlyList<OsvComponent>>([rows.Single(r => r.Scan.Id == scanId).Component]);
//     }
//     public Task CompleteAsync(Guid scanId, ScanCompletion completion, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         var row = rows.Single(r => r.Scan.Id == scanId);
//         if (row.Status != "Scanning") throw new InvalidOperationException("Scan ist nicht aktiv.");
//         var batch = (DemoTicketBatch)completion.Tickets;
//         row.Result = completion;
//         row.FinishedAt = completion.FinishedAt;
//         row.Status = "Scanned";
//         Tickets.AddRange(batch.Items);
//         return Task.CompletedTask;
//     }
//     public Task FailAsync(Guid scanId, string message, DateTimeOffset finishedAt, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         var row = rows.Single(r => r.Scan.Id == scanId);
//         row.Status = "Failed";
//         row.Error = message;
//         row.Result = null;
//         row.FinishedAt = finishedAt;
//         return Task.CompletedTask;
//     }
//     public Task<int> ResetInterruptedAsync(CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         var interrupted = rows.Where(r => r.Status == "Scanning").ToArray();
//         foreach (var row in interrupted)
//         {
//             row.Status = "Pending";
//             row.Error = null;
//             row.Result = null;
//             row.FinishedAt = null;
//         }
//         return Task.FromResult(interrupted.Length);
//     }
//     public Task<IReadOnlyList<IEscalationTicket>> GetDueTicketsAsync(DateTimeOffset now, CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         return Task.FromResult<IReadOnlyList<IEscalationTicket>>(Tickets.Where(t => !t.IsFinished &&
//             t.DueAt < now && t.EscalationLevel < 3).Cast<IEscalationTicket>().ToArray());
//     }
//     public void RecordEscalation(EscalationAuditEntry entry) => pendingAudit.Add(entry);
//     public Task SaveChangesAsync(CancellationToken ct)
//     {
//         ct.ThrowIfCancellationRequested();
//         Audit.AddRange(pendingAudit);
//         pendingAudit.Clear();
//         return Task.CompletedTask;
//     }
//     public void PrintScans()
//     {
//         foreach (var row in rows)
//             Console.WriteLine($"  {row.Name}: {row.Status}, Testurteil: {row.Result?.Verdict.ToString() ?? "KEINES"}.");
//     }
//     public void PrintTickets()
//     {
//         foreach (var ticket in Tickets)
//             Console.WriteLine($"  Demo-Ticket: Stufe {ticket.EscalationLevel}, {ticket.Role}, Frist: {ticket.DueAt?.ToString("O") ?? "keine"}.");
//     }
// }

// internal sealed class DemoTicket(DateTimeOffset dueAt) : IEscalationTicket
// {
//     public Guid Id { get; } = Guid.NewGuid();
//     public bool IsFinished => false;
//     public int EscalationLevel { get; private set; } = 1;
//     public DateTimeOffset? DueAt { get; private set; } = dueAt;
//     public string Role { get; private set; } = "Developer";
//     public void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt)
//     {
//         EscalationLevel = newLevel;
//         Role = assignedRole;
//         DueAt = newDueAt;
//     }
// }
