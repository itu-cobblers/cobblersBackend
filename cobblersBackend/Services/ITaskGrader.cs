using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// Evaluates a task's grading rules (Task.GradingJson — see SCHEMA.md
/// "Grading rules are data, evaluated by one backend engine") against an
/// execution result.
/// </summary>
public interface ITaskGrader
{
    /// <param name="gradingJson">The rule tree from Task.GradingJson (not null —
    /// callers skip grading entirely for tasks without rules).</param>
    Verdict Grade(string gradingJson, CheckResult result);
}
