namespace Worker.Enrichment;

public sealed class KevUnavailableException(string message) : Exception(message);
public sealed class EpssUnavailableException(string message) : Exception(message);
