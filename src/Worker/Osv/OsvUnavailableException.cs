namespace Worker.Osv;

public sealed class OsvUnavailableException(string message) : Exception(message);
