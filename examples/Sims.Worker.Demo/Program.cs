using Microsoft.Extensions.Logging;
using Worker;
using Worker.Enrichment;
using Worker.Escalation;
using Worker.Scanning;
using Sims.Worker.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("SIMS WORKER DEMO - ausschließlich erfundene Daten im Arbeitsspeicher.");
Console.WriteLine("Kein SQL, kein Redis, kein Internet. Urteile, Tickets und Fristen sind TESTANTWORTEN.");
Console.WriteLine("Geprüft wird unser Ablauf, nicht die noch fehlenden B4/C3/C4-Team-Regeln.\n");
using var logs = LoggerFactory.Create(builder => builder.AddProvider(new DemoLoggerProvider()));
var clock = new DemoClock();
var store = new DemoStore(clock.GetUtcNow());
var sources = new DemoSources();
var rules = new DemoRules();
var processor = new ScanProcessor(store, sources, new FindingEnricher(sources, sources),
    rules, rules, clock, logs.CreateLogger<ScanProcessor>());

Console.WriteLine("1. Neustart: Ein zuvor unterbrochener Scan steht noch auf Scanning.");
await new ScanRecovery(store, logs.CreateLogger<ScanRecovery>()).RecoverAsync(CancellationToken.None);
Console.WriteLine("2. Scan-Ablauf: Zwei Erfolge und ein absichtlich simulierter Quellenausfall.");
await processor.ProcessPendingAsync(CancellationToken.None);
store.PrintScans();
var callsBefore = sources.Calls;
await processor.ProcessPendingAsync(CancellationToken.None);
if (sources.Calls != callsBefore) throw new InvalidOperationException("Abgeschlossene Scans wurden erneut verarbeitet.");
Console.WriteLine("3. Zweiter Durchlauf: kein wartender Auftrag, keine erneuten Abfragen.");

var escalation = new EscalationProcessor(store, rules, store, clock, logs.CreateLogger<EscalationProcessor>());
Console.WriteLine("4. Demo-Uhr +60 Sekunden: vorbereitete Regelantwort Stufe 2 / Lead.");
clock.Advance(TimeSpan.FromSeconds(60));
await escalation.RunAsync(CancellationToken.None);
store.PrintTickets();
Console.WriteLine("5. Demo-Uhr +60 Sekunden: vorbereitete Regelantwort Stufe 3 / Admin.");
clock.Advance(TimeSpan.FromSeconds(60));
await escalation.RunAsync(CancellationToken.None);
await escalation.RunAsync(CancellationToken.None);
store.PrintTickets();

if (store.SuccessCount != 2 || store.FailureCount != 1 || store.Tickets.Count != 2 ||
    store.Tickets.Any(t => t.EscalationLevel != 3) || store.Audit.Count != 4)
    throw new InvalidOperationException("Demo-Endzustand entspricht nicht dem vorbereiteten Beispiel.");
Console.WriteLine($"\nDEMO ERFOLGREICH: {store.SuccessCount} Scanned, {store.FailureCount} Failed, " +
    $"{store.Tickets.Count} Demo-Tickets auf Stufe 3, {store.Audit.Count} Demo-Audit-Einträge.");
Console.WriteLine("Alle Daten verschwinden beim Beenden. Das beweist noch keine SQL- oder Team-Integration.");
