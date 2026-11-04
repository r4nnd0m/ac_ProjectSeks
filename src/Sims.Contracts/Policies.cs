/// Definiert die Namen für die Authorisierung

namespace Sims.Contracts;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string MfaRequired = "MfaRequired";
}
