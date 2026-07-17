using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// Evaluates an assignment's grading rules (Assignment.GradingJson — see SCHEMA.md
/// "Grading rules are data, evaluated by one backend engine") against an
/// execution result.
/// </summary>
public interface IAssignmentGrader
{
    /// <param name="gradingJson">The rule tree from Assignment.GradingJson (not null —
    /// callers skip grading entirely for assignments without rules).</param>
    Verdict Grade(string gradingJson, CheckResult result);
}
