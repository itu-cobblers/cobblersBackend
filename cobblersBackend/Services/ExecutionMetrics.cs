using cobblersBackend.DTOs;
using Prometheus;

namespace cobblersBackend.Services;

public class ExecutionMetrics : IExecutionMetrics
{
    private static readonly Counter ExecutionCounter = Metrics
        .CreateCounter(
            "cobblers_execute_requests_total",
            "Total number of classified code execution requests.",
            new CounterConfiguration { LabelNames = ["status"] });

    private static readonly Histogram ExecutionDurationHistogram = Metrics
        .CreateHistogram(
            "cobblers_execute_duration_seconds",
            "End-to-end /api/execute duration in seconds.",
            new HistogramConfiguration
            {
                LabelNames = ["status"],
                Buckets = Histogram.ExponentialBuckets(0.05, 2, 10)
            });

    private static readonly Histogram PistonDurationHistogram = Metrics
        .CreateHistogram(
            "cobblers_piston_request_duration_seconds",
            "Duration of outbound calls to Piston in seconds.",
            new HistogramConfiguration
            {
                LabelNames = ["outcome"],
                Buckets = Histogram.ExponentialBuckets(0.05, 2, 10)
            });

    public void ObserveExecutionResult(ExecuteStatus status, double durationSeconds)
    {
        var statusLabel = status.ToString().ToLowerInvariant();

        ExecutionCounter
            .WithLabels(statusLabel)
            .Inc();

        ExecutionDurationHistogram
            .WithLabels(statusLabel)
            .Observe(durationSeconds);
    }

    public void ObservePistonDuration(string outcome, double durationSeconds)
    {
        PistonDurationHistogram
            .WithLabels(outcome)
            .Observe(durationSeconds);
    }
}
