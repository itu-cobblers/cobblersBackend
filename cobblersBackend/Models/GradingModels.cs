namespace cobblersBackend.Models;

/// <summary>
/// The material a grading rule can inspect: the submitted source and the
/// execution result. Mirrors the frontend's old CheckResult shape.
/// </summary>
public record CheckResult(string Code, string Stdout, string Stderr, int? ExitCode);

/// <summary>
/// Server-computed grading verdict (feeds Submission.Passed). Feedback carries the
/// "message" text of every failing rule node that authored one — empty/null when
/// passed, or when the failing rule(s) didn't author a message.
/// </summary>
public record Verdict(bool Passed, IReadOnlyList<string>? Feedback = null);
