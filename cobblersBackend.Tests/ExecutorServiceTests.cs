using Moq;
using cobblersBackend.Services;
using cobblersBackend.Models;
using cobblersBackend.DTOs;

namespace cobblersBackend.Tests;

public sealed class ExecutorServiceTests
{
    private static Mock<IPistonClient> PistonReturning(PistonExecuteResponse response)
    {
        var fakePiston = new Mock<IPistonClient>();
        fakePiston
            .Setup(p => p.ExecuteAsync("java", It.IsAny<IReadOnlyList<PistonFile>>()))
            .ReturnsAsync(response);
        return fakePiston;
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunSucceeds_ReturnSuccess()
    {
        var fakePiston = PistonReturning(new PistonExecuteResponse
        {
            Run = new PistonStage("42\n", "", "42\n", 0, null)
        });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        var result = await service.ExecuteAsync("public class Main { ... }");

        Assert.Equal(ExecuteStatus.SUCCESS, result.Status);
        Assert.Equal("42\n", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompileFails_ReturnCompileError()
    {
        var fakePiston = PistonReturning(new PistonExecuteResponse
        {
            Run = new PistonStage("", "Main.java:3: error: ';' expected", "", 1, null)
        });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        var result = await service.ExecuteAsync("public class Main { ... }");

        Assert.Equal(ExecuteStatus.COMPILE_ERROR, result.Status);
        Assert.Equal("Main.java:3: error: ';' expected", result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuntimeFails_ReturnRuntimeError()
    {
        var fakePiston = PistonReturning(new PistonExecuteResponse
        {
            Run = new PistonStage("", "Exception in thread \"main\" java.lang.ArithmeticException: / by zero", "", 1, null)
        });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        var result = await service.ExecuteAsync("public class Main { ... }");

        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("Exception in thread \"main\" java.lang.ArithmeticException: / by zero", result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnrecognizedErr_ReturnRuntimeError()
    {
        var fakePiston = PistonReturning(new PistonExecuteResponse
        {
            Run = new PistonStage("", "killed", "", 137, null)
        });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        var result = await service.ExecuteAsync("public class Main { ... }");

        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("killed", result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPistonKillsAnInfiniteLoop_StudentGetsRuntimeErrorWithNoExplanation()
    {
        // Arrange — exactly what Piston returns for `while (true) {}`: it enforces its own
        // run timeout, kills the process, and reports code=null + signal=SIGKILL with
        // nothing on either stream (a SIGKILLed JVM gets no chance to print).
        var fakePiston = PistonReturning(new PistonExecuteResponse
        {
            Run = new PistonStage("", "", "", null, "SIGKILL")
        });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        var result = await service.ExecuteAsync("public class Main { public static void main(String[] a){ while(true){} } }");

        // Assert — CHARACTERIZATION of a known UX gap, not an endorsement. `code` isn't 0
        // so the classifier falls through its `// sigkill fallback` to RUNTIME_ERROR, and
        // both streams are empty, so the terminal renders "(no output)". A beginner who
        // wrote an infinite loop sees a blank panel and the word "error" with nothing
        // saying their program ran too long — arguably the single most likely mistake on
        // day 1. Fixing it means a distinct status (or at least a synthesized stderr
        // message) when signal is SIGKILL; flip this test then.
        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("", result.Stdout);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_SingleFile_SendsBareMainNameForPistonRename()
    {
        IReadOnlyList<PistonFile>? sent = null;
        var fakePiston = new Mock<IPistonClient>();
        fakePiston
            .Setup(p => p.ExecuteAsync("java", It.IsAny<IReadOnlyList<PistonFile>>()))
            .Callback<string, IReadOnlyList<PistonFile>>((_, files) => sent = files)
            .ReturnsAsync(new PistonExecuteResponse { Run = new PistonStage("", "", "", 0, null) });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        await service.ExecuteAsync("public class Main {}");

        Assert.NotNull(sent);
        Assert.Single(sent!);
        Assert.Equal("Main", sent![0].Name);
    }

    [Fact]
    public async Task ExecuteAsync_MultiFile_MergesIntoOneBareEntryFileAndDemotesHelpers()
    {
        IReadOnlyList<PistonFile>? sent = null;
        var fakePiston = new Mock<IPistonClient>();
        fakePiston
            .Setup(p => p.ExecuteAsync("java", It.IsAny<IReadOnlyList<PistonFile>>()))
            .Callback<string, IReadOnlyList<PistonFile>>((_, files) => sent = files)
            .ReturnsAsync(new PistonExecuteResponse { Run = new PistonStage("", "", "", 0, null) });
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), Mock.Of<IExecutionMetrics>());

        await service.ExecuteAsync(
        [
            new FileDto("FlightTicket.java", "public class FlightTicket { int price; }"),
            new FileDto("Main.java", "public class Main { public static void main(String[] a) { new FlightTicket(); } }"),
        ], "Main");

        Assert.NotNull(sent);
        Assert.Single(sent!);
        Assert.Equal("Main", sent![0].Name);
        Assert.Contains("public class Main", sent[0].Content);
        // Helper must be package-private so it can share the merged file with public Main.
        Assert.Contains("class FlightTicket", sent[0].Content);
        Assert.DoesNotContain("public class FlightTicket", sent[0].Content);
        // Entry file is first in the merged content even when it wasn't first in the request.
        Assert.StartsWith("public class Main", sent[0].Content.TrimStart());
    }
}
