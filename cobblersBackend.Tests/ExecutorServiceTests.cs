using Moq;
using cobblersBackend.Services;
using cobblersBackend.Models;
using cobblersBackend.DTOs;
namespace cobblersBackend.Tests;

public sealed class ExecutorServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRunSucceeds_ReturnSuccess()
    {
        // Arrange
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage("42\n", "", "42\n", 0, null)
            });

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), fakeMetrics.Object);

        // Act
        var result = await service.ExecuteAsync("public class Main { ... }");

        // Assert
        Assert.Equal(ExecuteStatus.SUCCESS, result.Status);
        Assert.Equal("42\n",result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompileFails_ReturnCompileError()
    {
        // Arrange
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage("", "Main.java:3: error: ';' expected", "", 1, null)
            });

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), fakeMetrics.Object);

        // Act
        var result = await service.ExecuteAsync("public class Main { ... }");

        // Assert
        Assert.Equal(ExecuteStatus.COMPILE_ERROR, result.Status);
        Assert.Equal("Main.java:3: error: ';' expected",result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuntimeFails_ReturnRuntimeError()
    {
        // Arrange
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage("", "Exception in thread \"main\" java.lang.ArithmeticException: / by zero", "", 1, null)
            });

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), fakeMetrics.Object);

        // Act
        var result = await service.ExecuteAsync("public class Main { ... }");

        // Assert
        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("Exception in thread \"main\" java.lang.ArithmeticException: / by zero",result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnrecognizedErr_ReturnRuntimeError()
    {
        // Arrange
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage("", "killed", "", 137, null)
            });

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), fakeMetrics.Object);

        // Act
        var result = await service.ExecuteAsync("public class Main { ... }");

        // Assert
        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("killed",result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPistonKillsAnInfiniteLoop_StudentGetsRuntimeErrorWithNoExplanation()
    {
        // Arrange — exactly what Piston returns for `while (true) {}`: it enforces its own
        // run timeout, kills the process, and reports code=null + signal=SIGKILL with
        // nothing on either stream (a SIGKILLed JVM gets no chance to print).
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage("", "", "", null, "SIGKILL")
            });

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier(), fakeMetrics.Object);

        // Act
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

    // two timeout tests in the executor test 
    // student supplied code cause timeout returned.
    // http timeout simulate the error path where the executor errors because piston is slow needs to be pinged to wake up or something 
}
