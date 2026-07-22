using Moq;
using cobblersBackend.DTOs;
using cobblersBackend.Models;
using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public class ExecutorServiceTest
{
    [Fact]
    public async Task ExecuteAsync_ReturnsClassifierResult_AndRecordsMetrics()
    {
        var fakePiston = new Mock<IPistonClient>();
        fakePiston
            .Setup(p => p.ExecuteAsync("java", It.IsAny<string>()))
            .ReturnsAsync(new PistonExecuteResponse
            {
                Run = new PistonStage { Code = 0, Stdout = "ok", Stderr = string.Empty }
            });

        var fakeClassifier = new Mock<IExecuteResultClassifier>();
        fakeClassifier
            .Setup(c => c.Classify(It.IsAny<PistonExecuteResponse>()))
            .Returns(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ok", string.Empty));

        var fakeMetrics = new Mock<IExecutionMetrics>();
        var service = new ExecutorService(fakePiston.Object, fakeClassifier.Object, fakeMetrics.Object);

        var result = await service.ExecuteAsync("public class Main {}");

        Assert.Equal(ExecuteStatus.SUCCESS, result.Status);
        fakeMetrics.Verify(
            metrics => metrics.ObserveExecutionResult(
                ExecuteStatus.SUCCESS,
                It.Is<double>(duration => duration >= 0)),
            Times.Once);
    }
}
