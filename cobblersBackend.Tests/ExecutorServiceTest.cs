using Moq;
using cobblersBackend.Services;
using cobblersBackend.Models;
using cobblersBackend.DTOs;
namespace cobblersBackend.Tests;

public class ExecutorServiceTest
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
        
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier());

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
        
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier());

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
        
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier());

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
        
        var service = new ExecutorService(fakePiston.Object, new JavaExecuteResultClassifier());

        // Act
        var result = await service.ExecuteAsync("public class Main { ... }");

        // Assert
        Assert.Equal(ExecuteStatus.RUNTIME_ERROR, result.Status);
        Assert.Equal("killed",result.Stderr);
    }
}
