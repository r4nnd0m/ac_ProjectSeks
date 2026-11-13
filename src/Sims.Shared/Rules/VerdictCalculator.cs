using Microsoft.Extensions.Options;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace Sims.Shared.Rules;

public sealed class VerdictOptions
{
    public double EpssNoGoThreshold { get; set; } = 0.5;
}

public interface IVerdictRule
{
    Verdict? Evaluate(IReadOnlyList<Finding> findings);
}

public sealed class KevRule : IVerdictRule
{
    public Verdict? Evaluate(IReadOnlyList<Finding> findings) =>
        findings.Any(f => f.IsKev) ? Verdict.NoGo : null;
}

public sealed class CvssRule : IVerdictRule
{
    public Verdict? Evaluate(IReadOnlyList<Finding> findings)
    {
        if (findings.Any(f => f.Severity == Severity.Critical)) return Verdict.NoGo;
        return findings.Any(f => f.Severity is Severity.High or Severity.Medium)
            ? Verdict.GoWithConditions : null;
    }
}

public sealed class EpssRule(IOptions<VerdictOptions> options) : IVerdictRule
{
    public Verdict? Evaluate(IReadOnlyList<Finding> findings) =>
        findings.Any(f => f.Epss >= options.Value.EpssNoGoThreshold) ? Verdict.NoGo : null;
}

public sealed class VerdictCalculator(IEnumerable<IVerdictRule> rules)
{
    public Verdict Calculate(IReadOnlyList<Finding> findings) =>
        rules.Select(rule => rule.Evaluate(findings)).Max() ?? Verdict.Go;
}
