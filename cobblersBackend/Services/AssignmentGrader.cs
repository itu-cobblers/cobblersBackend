using System.Text.Json;
using System.Text.RegularExpressions;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// The one generic evaluator for Assignment.GradingJson rule trees (see SCHEMA.md
/// "Grading rules are data, evaluated by one backend engine"). A node is one of:
///
///   { "all": [node, ...] }   { "any": [node, ...] }   { "not": node }
///   { "target": "stdout"|"code", "op": "contains",     "value": "..." }
///   { "target": "stdout",        "op": "containsLine", "value": "..." }
///   { "target": "stdout"|"code", "op": "regex", "pattern": "...", "flags": "i"? }
///   { "op": "nonEmptyStdout" }
///   { "op": "custom", "key": "&lt;slug&gt;" }   // escape hatch, C# registry keyed by slug
///   { "predict": { "compare": "normalized"|"exact", "expectedOutput": "...", "accept"?: [...] } }
///
/// Any node (combinator or leaf) may also carry a "message" string. It plays no
/// part in pass/fail logic — it's surfaced via Verdict.Feedback when that node
/// is the reason a submission failed, so students get "why", not just a bool.
///
/// For code rules, grading only runs on a successful execution — non-zero (or
/// missing) exit code fails before any rule is evaluated. Predict documents
/// skip execution and compare CheckResult.Code to expectedOutput. Output
/// matching mirrors the frontend's old grade.ts / predict.ts semantics:
/// lenient about surrounding whitespace, otherwise faithful.
///
/// Malformed rules throw ArgumentException — a broken seed should fail loudly,
/// not silently pass/fail students.
/// </summary>
public class AssignmentGrader : IAssignmentGrader
{
    private readonly IReadOnlyDictionary<string, Func<CheckResult, bool>> _customChecks;

    public AssignmentGrader(IReadOnlyDictionary<string, Func<CheckResult, bool>>? customChecks = null)
    {
        _customChecks = customChecks ?? new Dictionary<string, Func<CheckResult, bool>>();
    }

    public Verdict Grade(string gradingJson, CheckResult result)
    {
        using var doc = JsonDocument.Parse(gradingJson);

        // Predict quizzes use a dedicated document shape (see seed grading_json):
        //   { "predict": { "compare": "normalized", "expectedOutput": "...", "accept"?: [...] } }
        // No Piston run — the student's typed answer is in CheckResult.Code.
        if (doc.RootElement.TryGetProperty("predict", out var predict))
            return new Verdict(EvaluatePredict(predict, result.Code));

        // A non-zero/missing exit code fails before any rule runs — the raw
        // status/stderr on the execution result already tells the student their
        // code didn't compile/run, so no rule-level message is synthesized here.
        if (result.ExitCode is not 0)
            return new Verdict(false);

        var (passed, messages) = Evaluate(doc.RootElement, result);
        return new Verdict(passed, messages.Count > 0 ? messages : null);
    }

    /// <summary>
    /// Compare a predict-quiz answer to expectedOutput (+ optional accept phrases),
    /// mirroring the frontend's old predict.ts semantics.
    /// </summary>
    private static bool EvaluatePredict(JsonElement predict, string answer)
    {
        if (predict.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Predict grading rule must be an object, got {predict.ValueKind}.");

        var compare = predict.TryGetProperty("compare", out var compareElement)
            ? compareElement.GetString() ?? "normalized"
            : "normalized";
        var expected = RequiredString(predict, "expectedOutput");

        var matches = compare switch
        {
            "normalized" => NormalizeOutput(answer) == NormalizeOutput(expected),
            "exact" => answer == expected,
            _ => throw new ArgumentException($"Unknown predict compare mode '{compare}'."),
        };
        if (matches) return true;

        if (!predict.TryGetProperty("accept", out var accept) || accept.ValueKind != JsonValueKind.Array)
            return false;

        var lower = NormalizeOutput(answer).ToLowerInvariant();
        foreach (var phrase in accept.EnumerateArray())
        {
            if (phrase.ValueKind != JsonValueKind.String) continue;
            var needle = (phrase.GetString() ?? "").Trim().ToLowerInvariant();
            if (needle.Length > 0 && lower.Contains(needle)) return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates a rule node, returning both the boolean verdict and the
    /// "message" text (see class doc) of whichever node(s) failed. A node may
    /// carry an optional string "message" property alongside "all"/"any"/"not"/
    /// leaf-op — it's pure authoring metadata, never consulted by the pass/fail
    /// logic itself, only bubbled up here so a failing submission can tell the
    /// student *why*.
    /// </summary>
    private (bool Passed, List<string> Messages) Evaluate(JsonElement node, CheckResult result)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Grading rule must be an object, got {node.ValueKind}.");

        if (node.TryGetProperty("all", out var all))
        {
            // Every child is evaluated (no short-circuit) so a failing "all"
            // reports all of its unmet requirements at once, not just the first.
            var messages = new List<string>();
            var passed = true;
            foreach (var child in all.EnumerateArray())
            {
                var (childPassed, childMessages) = Evaluate(child, result);
                if (childPassed) continue;
                passed = false;
                messages.AddRange(childMessages);
            }
            return (passed, messages);
        }

        if (node.TryGetProperty("any", out var any))
        {
            var childResults = any.EnumerateArray().Select(child => Evaluate(child, result)).ToList();
            if (childResults.Any(r => r.Passed)) return (true, new List<string>());

            // All branches failed: prefer the node's own message (one clear combined
            // sentence written by the author) over dumping every branch's message.
            var messages = NodeMessage(node) is { } ownMessage
                ? new List<string> { ownMessage }
                : childResults.SelectMany(r => r.Messages).ToList();
            return (false, messages);
        }

        if (node.TryGetProperty("not", out var not))
        {
            var (childPassed, _) = Evaluate(not, result);
            var passed = !childPassed;
            var messages = passed || NodeMessage(node) is not { } ownMessage
                ? new List<string>()
                : new List<string> { ownMessage };
            return (passed, messages);
        }

        var op = node.TryGetProperty("op", out var opElement)
            ? opElement.GetString()
            : throw new ArgumentException("Grading rule has neither a combinator (all/any/not) nor an op.");

        var leafPassed = op switch
        {
            "contains" => Target(node, result).Contains(RequiredString(node, "value")),
            "containsLine" => ContainsLine(Target(node, result), RequiredString(node, "value")),
            "regex" => Regex.IsMatch(Target(node, result), RequiredString(node, "pattern"), RegexOptions(node)),
            "nonEmptyStdout" => result.Stdout.Trim().Length > 0,
            "custom" => Custom(RequiredString(node, "key"), result),
            _ => throw new ArgumentException($"Unknown grading op '{op}'."),
        };

        var leafMessages = leafPassed || NodeMessage(node) is not { } leafMessage
            ? new List<string>()
            : new List<string> { leafMessage };
        return (leafPassed, leafMessages);
    }

    private static string? NodeMessage(JsonElement node) =>
        node.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()
            : null;

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
