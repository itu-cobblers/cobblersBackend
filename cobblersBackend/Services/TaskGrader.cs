using System.Text.Json;
using System.Text.RegularExpressions;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// The one generic evaluator for Task.GradingJson rule trees (see SCHEMA.md
/// "Grading rules are data, evaluated by one backend engine"). A node is one of:
///
///   { "all": [node, ...] }   { "any": [node, ...] }   { "not": node }
///   { "target": "stdout"|"code", "op": "contains",     "value": "..." }
///   { "target": "stdout",        "op": "containsLine", "value": "..." }
///   { "target": "stdout"|"code", "op": "regex", "pattern": "...", "flags": "i"? }
///   { "op": "nonEmptyStdout" }
///   { "op": "custom", "key": "&lt;slug&gt;" }   // escape hatch, C# registry keyed by slug
///
/// Grading only runs on a successful execution — non-zero (or missing) exit
/// code fails before any rule is evaluated. Output matching mirrors the
/// frontend's old grade.ts semantics: lenient about surrounding whitespace,
/// otherwise faithful.
///
/// Malformed rules throw ArgumentException — a broken seed should fail loudly,
/// not silently pass/fail students.
/// </summary>
public class TaskGrader : ITaskGrader
{
    private readonly IReadOnlyDictionary<string, Func<CheckResult, bool>> _customChecks;

    public TaskGrader(IReadOnlyDictionary<string, Func<CheckResult, bool>>? customChecks = null)
    {
        _customChecks = customChecks ?? new Dictionary<string, Func<CheckResult, bool>>();
    }

    public Verdict Grade(string gradingJson, CheckResult result)
    {
        if (result.ExitCode is not 0)
            return new Verdict(false);

        using var doc = JsonDocument.Parse(gradingJson);
        return new Verdict(Evaluate(doc.RootElement, result));
    }

    private bool Evaluate(JsonElement node, CheckResult result)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Grading rule must be an object, got {node.ValueKind}.");

        if (node.TryGetProperty("all", out var all))
            return all.EnumerateArray().All(child => Evaluate(child, result));

        if (node.TryGetProperty("any", out var any))
            return any.EnumerateArray().Any(child => Evaluate(child, result));

        if (node.TryGetProperty("not", out var not))
            return !Evaluate(not, result);

        var op = node.TryGetProperty("op", out var opElement)
            ? opElement.GetString()
            : throw new ArgumentException("Grading rule has neither a combinator (all/any/not) nor an op.");

        return op switch
        {
            "contains" => Target(node, result).Contains(RequiredString(node, "value")),
            "containsLine" => ContainsLine(Target(node, result), RequiredString(node, "value")),
            "regex" => Regex.IsMatch(Target(node, result), RequiredString(node, "pattern"), RegexOptions(node)),
            "nonEmptyStdout" => result.Stdout.Trim().Length > 0,
            "custom" => Custom(RequiredString(node, "key"), result),
            _ => throw new ArgumentException($"Unknown grading op '{op}'."),
        };
    }

    /// <summary>What the rule inspects: normalized stdout (default) or the raw submitted code.</summary>
    private static string Target(JsonElement node, CheckResult result)
    {
        var target = node.TryGetProperty("target", out var t) ? t.GetString() : "stdout";
        return target switch
        {
            "stdout" => NormalizeOutput(result.Stdout),
            "code" => result.Code,
            _ => throw new ArgumentException($"Unknown grading target '{target}'."),
        };
    }

    private static string RequiredString(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new ArgumentException($"Grading rule is missing string property '{property}'.");

    private static RegexOptions RegexOptions(JsonElement node)
    {
        var flags = node.TryGetProperty("flags", out var f) ? f.GetString() ?? "" : "";
        return flags.Contains('i')
            ? System.Text.RegularExpressions.RegexOptions.IgnoreCase
            : System.Text.RegularExpressions.RegexOptions.None;
    }

    private bool Custom(string key, CheckResult result) =>
        _customChecks.TryGetValue(key, out var check)
            ? check(result)
            : throw new ArgumentException($"No custom grading check registered for key '{key}'.");

    /// <summary>Trim trailing whitespace per line, drop leading/trailing blank lines (grade.ts parity).</summary>
    private static string NormalizeOutput(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }

    /// <summary>True if some line of the normalized output equals the value (both trimmed).</summary>
    private static bool ContainsLine(string normalizedOutput, string value)
    {
        var target = value.Trim();
        return normalizedOutput.Split('\n').Any(line => line.Trim() == target);
    }
}
