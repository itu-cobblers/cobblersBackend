using Moq;
using cobblersBackend.DTOs;
using cobblersBackend.Models;
using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public class ExecutorServiceTest
{
    [Fact]
    public async Task ExecuteAsync_WhenCompileFails_ReturnCompileError()
    {
        // This Piston deployment collapses compile+run into the run stage
        // (Compile stays null) — the classifier sniffs run.Stderr instead.
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage(
                    Stdout: "",
                    Stderr: "Main.java:3: error: ';' expected",
                    Output: "",
                    Code: 1,
                    Signal: null)
            });

        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier());
        var result = await service.ExecuteAsync("not java");

        Assert.Equal(ExecuteStatus.COMPILE_ERROR, result.Status);
        Assert.Contains("error", result.Stderr);
    }
}
