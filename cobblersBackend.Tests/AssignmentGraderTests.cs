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

    [Fact]
    public void Regex_SinglineFlag_LetsDotMatchNewlinesAcrossMultilineCode()
    {
        var multilineIf = "if (a) {\n    System.out.println(1);\n}";

        var withSFlag = """{"target": "code", "op": "regex", "flags": "s", "pattern": "if\\s*\\(\\s*a\\s*\\)\\s*\\{.*println"}""";
        Assert.True(Grader.Grade(withSFlag, WithCode(multilineIf)).Passed);

        // Without "s", "." doesn't match "\n" — the same pattern can't span the line break.
        var withoutSFlag = """{"target": "code", "op": "regex", "pattern": "if\\s*\\(\\s*a\\s*\\)\\s*\\{.*println"}""";
        Assert.False(Grader.Grade(withoutSFlag, WithCode(multilineIf)).Passed);
    }

    [Fact]
    public void Regex_CanEnforceMultilineNestedIfStructure_RejectsFlatEquivalent()
    {
        // canteen-lunch's real seeded rule: nested if, three layers deep, no || shortcut allowed.
        var rule = """
            {"target": "code", "op": "regex", "flags": "s", "pattern": "if\\s*\\(\\s*isWeekday\\s*\\)\\s*\\{\\s*if\\s*\\(\\s*time\\s*<\\s*11\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*System\\.out\\.println\\((?:(?!\\}).)*\\)(?:(?!\\}).)*\\}\\s*else\\s*\\{\\s*if\\s*\\(\\s*time\\s*>=\\s*14\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*System\\.out\\.println\\((?:(?!\\}).)*\\)(?:(?!\\}).)*\\}\\s*else\\s*\\{(?:(?!\\}).)*System\\.out\\.println\\((?:(?!\\}).)*\\)(?:(?!\\}).)*\\}\\s*\\}\\s*\\}\\s*else\\s*\\{(?:(?!\\}).)*System\\.out\\.println\\((?:(?!\\}).)*\\)(?:(?!\\}).)*\\}"}
            """;

        var nested = """
            if (isWeekday) {
                if (time < 11.0) {
                    System.out.println("Close");
                } else {
                    if (time >= 14.0) {
                        System.out.println("Close");
                    } else {
                        System.out.println("Open");
                    }
                }
            } else {
                System.out.println("Close");
            }
            """;
        Assert.True(Grader.Grade(rule, WithCode(nested)).Passed);

        // Same behavior, but flattened into one condition with && — no actual nesting.
        var flat = """
            if (isWeekday && time >= 11.0 && time < 14.0) {
                System.out.println("Open");
            } else {
                System.out.println("Close");
            }
            """;
        Assert.False(Grader.Grade(rule, WithCode(flat)).Passed);
    }

    [Fact]
    public void Any_AcceptsEitherThresholdDirection_ForCanteenLunchDiscount()
    {
        // canteen-lunch-discount's real seeded rule: the reference solution checks
        // largest-to-smallest (>= 14.0 first), but a smallest-to-largest ladder
        // (< 11.0 first) is equally correct and must also pass — the "any" lets
        // either direction satisfy the structural check.
        var rule = """
            {"all": [
              {"target": "code", "op": "regex", "flags": "i", "pattern": "printLunchStatus\\s*\\(\\s*double\\s+time\\s*\\)"},
              {"any": [
                {"target": "code", "op": "regex", "flags": "s", "pattern": "if\\s*\\(\\s*time\\s*>=\\s*14\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s+if\\s*\\(\\s*time\\s*>=\\s*13\\.75\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s+if\\s*\\(\\s*time\\s*>=\\s*11\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s*\\{(?:(?!\\}).)*\\}"},
                {"target": "code", "op": "regex", "flags": "s", "pattern": "if\\s*\\(\\s*time\\s*<\\s*11\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s+if\\s*\\(\\s*time\\s*<\\s*13\\.75\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s+if\\s*\\(\\s*time\\s*<\\s*14\\.0\\s*\\)\\s*\\{(?:(?!\\}).)*\\}\\s*else\\s*\\{(?:(?!\\}).)*\\}"}
              ]},
              {"target": "stdout", "op": "regex", "pattern": "^Too early - lunch starts at 11:00\\.\\nLunch is being served at full price\\.\\nLunch is being served with a late lunch discount!\\nToo late - lunch ended at 14:00\\.$"}
            ]}
            """;

        var descending = """
            static void printLunchStatus(double time) {
                if (time >= 14.0) {
                    System.out.println("Too late - lunch ended at 14:00.");
                } else if (time >= 13.75) {
                    System.out.println("Lunch is being served with a late lunch discount!");
                } else if (time >= 11.0) {
                    System.out.println("Lunch is being served at full price.");
                } else {
                    System.out.println("Too early - lunch starts at 11:00.");
                }
            }
            """;
        var descendingStdout = "Too early - lunch starts at 11:00.\nLunch is being served at full price.\nLunch is being served with a late lunch discount!\nToo late - lunch ended at 14:00.";
        Assert.True(Grader.Grade(rule, new CheckResult(descending, descendingStdout, "", 0)).Passed);

        var ascending = """
            static void printLunchStatus(double time) {
                if (time < 11.0) {
                    System.out.println("Too early - lunch starts at 11:00.");
                } else if (time < 13.75) {
                    System.out.println("Lunch is being served at full price.");
                } else if (time < 14.0) {
                    System.out.println("Lunch is being served with a late lunch discount!");
                } else {
                    System.out.println("Too late - lunch ended at 14:00.");
                }
            }
            """;
        Assert.True(Grader.Grade(rule, new CheckResult(ascending, descendingStdout, "", 0)).Passed);
    }

    [Fact]
    public void Regex_OrderSensitiveStdoutCheck_CatchesSwappedIfElseBranches()
    {
        // scrollbar-friday's real seeded rule: containsLine alone can't tell the two
        // required substrings apart from which call produced them, so a student who
        // swaps the if/else bodies (Friday -> "closed", Thursday -> "open") still has
        // both substrings present — just paired with the wrong call. The rule instead
        // requires "Yes" to appear before "No" in program output order (substring only,
        // not the full sentence — students can phrase the message however they like).
        var rule = """{"target": "stdout", "op": "regex", "flags": "s", "pattern": "Yes.*No"}""";

        // IsScrollBarOpen("Friday") then IsScrollBarOpen("Thursday"), correct branches.
        var correct = "Yes, it is Friday, Scrollbar will open today!\nNo, Scrollbar is closed.";
        Assert.True(Grader.Grade(rule, WithStdout(correct)).Passed);

        // Same two calls, but the if/else bodies are swapped — logic is reversed.
        var swapped = "No, Scrollbar is closed.\nYes, it is Friday, Scrollbar will open today!";
        Assert.False(Grader.Grade(rule, WithStdout(swapped)).Passed);
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

    // ── predict quizzes ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("10\n9\n8", true)]
    [InlineData("10  \n9\n8\n", true)]   // trailing whitespace / blank lines tolerated
    [InlineData("  10\n9\n8", false)]    // leading indent on a line is significant
    [InlineData("10\n9\n7", false)]
    public void Predict_NormalizedCompare_MatchesExpectedOutput(string answer, bool expected)
    {
        var rule = """
            {"predict": {"compare": "normalized", "expectedOutput": "10\n9\n8"}}
            """;
        Assert.Equal(expected, Grader.Grade(rule, WithCode(answer)).Passed);
    }

    [Fact]
    public void Predict_AcceptPhrases_MatchCaseInsensitiveSubstring()
    {
        var rule = """
            {"predict": {
              "compare": "normalized",
              "expectedOutput": "infinite loop",
              "accept": ["never stops", "forever"]
            }}
            """;
        Assert.True(Grader.Grade(rule, WithCode("it Never Stops running")).Passed);
        Assert.True(Grader.Grade(rule, WithCode("loops forever")).Passed);
        Assert.False(Grader.Grade(rule, WithCode("stops eventually")).Passed);
    }

    [Fact]
    public void Predict_ExactCompare_RequiresByteForByteMatch()
    {
        var rule = """{"predict": {"compare": "exact", "expectedOutput": "10\n9"}}""";
        Assert.True(Grader.Grade(rule, WithCode("10\n9")).Passed);
        Assert.False(Grader.Grade(rule, WithCode("10\n9\n")).Passed);
    }

    // ── per-rule feedback messages ──────────────────────────────────────────

    [Fact]
    public void Leaf_FailureWithMessage_SurfacesItAsFeedback()
    {
        var rule = """{"target": "stdout", "op": "contains", "value": "2024", "message": "Print the year 2024."}""";
        var verdict = Grader.Grade(rule, WithStdout("no year here"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "Print the year 2024." }, verdict.Feedback);
    }

    [Fact]
    public void Leaf_FailureWithoutMessage_HasNullFeedback()
    {
        var rule = """{"target": "stdout", "op": "contains", "value": "2024"}""";
        var verdict = Grader.Grade(rule, WithStdout("no year here"));
        Assert.False(verdict.Passed);
        Assert.Null(verdict.Feedback);
    }

    [Fact]
    public void Leaf_Passing_HasNullFeedback()
    {
        var rule = """{"target": "stdout", "op": "contains", "value": "2024", "message": "Print the year 2024."}""";
        var verdict = Grader.Grade(rule, WithStdout("2024"));
        Assert.True(verdict.Passed);
        Assert.Null(verdict.Feedback);
    }

    [Fact]
    public void All_BubblesEveryFailingChildsMessage()
    {
        var rule = """
            {"all": [
              {"target": "stdout", "op": "contains", "value": "2024", "message": "Missing the year."},
              {"target": "stdout", "op": "contains", "value": "-273.15", "message": "Missing absolute zero."},
              {"target": "stdout", "op": "contains", "value": "hello"}
            ]}
            """;
        var verdict = Grader.Grade(rule, WithStdout("nothing relevant"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "Missing the year.", "Missing absolute zero." }, verdict.Feedback);
    }

    [Fact]
    public void All_OnePassingChild_OnlyBubblesTheFailingOnes()
    {
        var rule = """
            {"all": [
              {"target": "stdout", "op": "contains", "value": "2024", "message": "Missing the year."},
              {"target": "stdout", "op": "contains", "value": "-273.15", "message": "Missing absolute zero."}
            ]}
            """;
        var verdict = Grader.Grade(rule, WithStdout("2024"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "Missing absolute zero." }, verdict.Feedback);
    }

    [Fact]
    public void Any_AllFail_PrefersItsOwnMessageOverChildren()
    {
        var rule = """
            {"any": [
              {"target": "stdout", "op": "containsLine", "value": "Hello World!", "message": "child a"},
              {"target": "stdout", "op": "containsLine", "value": "Hello, World!", "message": "child b"}
            ], "message": "Print either \"Hello World!\" or \"Hello, World!\"."}
            """;
        var verdict = Grader.Grade(rule, WithStdout("hello world"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "Print either \"Hello World!\" or \"Hello, World!\"." }, verdict.Feedback);
    }

    [Fact]
    public void Any_AllFail_NoOwnMessage_FallsBackToChildren()
    {
        var rule = """
            {"any": [
              {"target": "stdout", "op": "containsLine", "value": "Hello World!", "message": "child a"},
              {"target": "stdout", "op": "containsLine", "value": "Hello, World!", "message": "child b"}
            ]}
            """;
        var verdict = Grader.Grade(rule, WithStdout("hello world"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "child a", "child b" }, verdict.Feedback);
    }

    [Fact]
    public void Any_OneChildPasses_HasNullFeedback()
    {
        var rule = """
            {"any": [
              {"target": "stdout", "op": "containsLine", "value": "Hello World!", "message": "child a"},
              {"target": "stdout", "op": "containsLine", "value": "Hello, World!", "message": "child b"}
            ], "message": "own message"}
            """;
        var verdict = Grader.Grade(rule, WithStdout("Hello World!"));
        Assert.True(verdict.Passed);
        Assert.Null(verdict.Feedback);
    }

    [Fact]
    public void Not_UnexpectedlyTrue_SurfacesItsOwnMessage()
    {
        var rule = """{"not": {"target": "stdout", "op": "regex", "pattern": "-\\d+\\s*DKK"}, "message": "Price must never go negative."}""";
        var verdict = Grader.Grade(rule, WithStdout("CPH --> JFK (-3000 DKK)"));
        Assert.False(verdict.Passed);
        Assert.Equal(new[] { "Price must never go negative." }, verdict.Feedback);
    }

    [Fact]
    public void Not_Passing_HasNullFeedback()
    {
        var rule = """{"not": {"target": "stdout", "op": "regex", "pattern": "-\\d+\\s*DKK"}, "message": "Price must never go negative."}""";
        var verdict = Grader.Grade(rule, WithStdout("CPH --> JFK (0 DKK)"));
        Assert.True(verdict.Passed);
        Assert.Null(verdict.Feedback);
    }

    [Fact]
    public void Grade_NonZeroExitCode_HasNullFeedbackEvenWithMessages()
    {
        var rule = """{"op": "nonEmptyStdout", "message": "should never be seen"}""";
        var verdict = Grader.Grade(rule, WithStdout("plenty of output", exitCode: 1));
        Assert.False(verdict.Passed);
        Assert.Null(verdict.Feedback);
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
