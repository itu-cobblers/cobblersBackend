using cobblersBackend.Models;
using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public class AssignmentGraderTests
{
    private static readonly AssignmentGrader Grader = new();

    private static CheckResult WithStdout(string stdout, int? exitCode = 0) =>
        new(Code: "", Stdout: stdout, Stderr: "", ExitCode: exitCode);

    private static CheckResult WithCode(string code) =>
        new(Code: code, Stdout: "", Stderr: "", ExitCode: 0);

    // ── execution gate ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    public void Grade_NonZeroOrMissingExitCode_FailsBeforeRules(int? exitCode)
    {
        var verdict = Grader.Grade("""{"op": "nonEmptyStdout"}""", WithStdout("plenty of output", exitCode));
        Assert.False(verdict.Passed);
    }

    // ── leaf ops ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("100 kr corresponds to 13.42 euro", true)]
    [InlineData("no currency here", false)]
    public void Contains_MatchesSubstringOfStdout(string stdout, bool expected)
    {
        var rule = """{"target": "stdout", "op": "contains", "value": "kr corresponds to"}""";
        Assert.Equal(expected, Grader.Grade(rule, WithStdout(stdout)).Passed);
    }

    [Theory]
    [InlineData("5\n50\n", true)]     // exact line (trailing newline tolerated)
    [InlineData("  50  \n", true)]    // surrounding whitespace tolerated
    [InlineData("150\n", false)]      // substring of a line is NOT a line match
    public void ContainsLine_MatchesWholeTrimmedLinesOnly(string stdout, bool expected)
    {
        var rule = """{"target": "stdout", "op": "containsLine", "value": "50"}""";
        Assert.Equal(expected, Grader.Grade(rule, WithStdout(stdout)).Passed);
    }

    [Fact]
    public void Regex_OnStdout_HonoursIgnoreCaseFlag()
    {
        var rule = """{"target": "stdout", "op": "regex", "pattern": "bmi", "flags": "i"}""";
        Assert.True(Grader.Grade(rule, WithStdout("Your BMI is 22.2")).Passed);

        var caseSensitive = """{"target": "stdout", "op": "regex", "pattern": "bmi"}""";
        Assert.False(Grader.Grade(caseSensitive, WithStdout("Your BMI is 22.2")).Passed);
    }

    [Fact]
    public void Regex_OnCode_InspectsSubmittedSourceNotOutput()
    {
        var rule = """{"target": "code", "op": "regex", "pattern": "c2f\\s*\\("}""";
        Assert.True(Grader.Grade(rule, WithCode("static void c2f (double c) {}")).Passed);
        Assert.False(Grader.Grade(rule, WithCode("static void f2c(double f) {}")).Passed);
    }

    [Theory]
    [InlineData("My Cozy Café", true)]
    [InlineData("   \n  ", false)]
    public void NonEmptyStdout_RequiresVisibleOutput(string stdout, bool expected)
    {
        Assert.Equal(expected, Grader.Grade("""{"op": "nonEmptyStdout"}""", WithStdout(stdout)).Passed);
    }

    // ── combinators ─────────────────────────────────────────────────────────

    [Fact]
    public void All_RequiresEveryChild()
    {
        var rule = """
            {"all": [
              {"target": "stdout", "op": "contains", "value": "2024"},
              {"target": "stdout", "op": "contains", "value": "-273.15"}
            ]}
            """;
        Assert.True(Grader.Grade(rule, WithStdout("2024\nhello\n-273.15")).Passed);
        Assert.False(Grader.Grade(rule, WithStdout("2024 only")).Passed);
    }

    [Fact]
    public void Any_AcceptsEitherSpelling()
    {
        // any-combinator example (accept either spelling)
        var rule = """
            {"any": [
              {"target": "stdout", "op": "containsLine", "value": "Hello World!"},
              {"target": "stdout", "op": "containsLine", "value": "Hello, World!"}
            ]}
            """;
        Assert.True(Grader.Grade(rule, WithStdout("Hello, World!\n")).Passed);
        Assert.True(Grader.Grade(rule, WithStdout("Hello World!\n")).Passed);
        Assert.False(Grader.Grade(rule, WithStdout("hello world\n")).Passed);
    }

    [Fact]
    public void Not_InvertsItsChild()
    {
        // flight-ticket-class's real seeded rule: price must never print negative
        var rule = """
            {"all": [
              {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7500 DKK)"},
              {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7000 DKK)"},
              {"not": {"target": "stdout", "op": "regex", "pattern": "-\\d+\\s*DKK"}}
            ]}
            """;
        var good = "CPH --> JFK (7500 DKK)\nCPH --> JFK (7000 DKK)\nCPH --> JFK (0 DKK)";
        var abused = "CPH --> JFK (7500 DKK)\nCPH --> JFK (7000 DKK)\nCPH --> JFK (-3000 DKK)";
        Assert.True(Grader.Grade(rule, WithStdout(good)).Passed);
        Assert.False(Grader.Grade(rule, WithStdout(abused)).Passed);
    }

    // ── custom escape hatch ─────────────────────────────────────────────────

    [Fact]
    public void Custom_ResolvesRegisteredCheckBySlug()
    {
        var grader = new AssignmentGrader(new Dictionary<string, Func<CheckResult, bool>>
        {
            ["some-task"] = r => r.Stdout.StartsWith("ok"),
        });
        var rule = """{"op": "custom", "key": "some-task"}""";
        Assert.True(grader.Grade(rule, WithStdout("ok then")).Passed);
        Assert.False(grader.Grade(rule, WithStdout("nope")).Passed);
    }

    [Fact]
    public void Custom_UnregisteredKey_ThrowsLoudly()
    {
        Assert.Throws<ArgumentException>(() =>
            Grader.Grade("""{"op": "custom", "key": "nobody-home"}""", WithStdout("x")));
    }

    // ── misconfiguration is loud, not a silent verdict ──────────────────────

    [Theory]
    [InlineData("""{"op": "frobnicate"}""")]
    [InlineData("""{"value": "no op or combinator"}""")]
    [InlineData("""{"target": "stderr", "op": "contains", "value": "x"}""")]
    [InlineData("""{"op": "contains", "target": "stdout"}""")]
    public void Grade_MalformedRule_Throws(string rule)
    {
        Assert.Throws<ArgumentException>(() => Grader.Grade(rule, WithStdout("x")));
    }
}
