using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public interface IExecutionMetrics
{
    void ObserveExecutionResult(ExecuteStatus status, double durationSeconds);
    void ObservePistonDuration(string outcome, double durationSeconds);
}
