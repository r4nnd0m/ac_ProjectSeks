namespace Worker.Osv;

/// <summary>Der Scan darf mit dieser unvollständigen Datenlage kein Urteil erhalten.</summary>
public sealed class OsvUnavailableException(string message) : Exception(message);
