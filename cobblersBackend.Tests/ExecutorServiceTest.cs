using Moq;
using cobblersBackend.Services;
using cobblersBackend.Models;
namespace cobblersBackend.Tests;

public class ExecutorServiceTest
{  
    [Fact]
    public async Task ExecuteAsync_WhenCompileFails_ReturnCompileError()
    {
        var fakePiston = new Mock<IPistonClient>();
        fakePiston.Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Compile = new PistonStage { Code = 1, Stderr = "error: ';' expected" },
                Run = new PistonStage()
            });

        var result = await new ExecutorService(fakePiston.Object).ExecuteAsync("not java");

        Assert.Contains("error", result);
        
    }
}
