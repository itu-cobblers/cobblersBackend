using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

/// <summary>Read-side queries behind CONTRACT.md's Tasks endpoints.</summary>
public interface ITaskSetService
{
    Task<IReadOnlyList<TaskSetSummaryDto>> ListTaskSetsAsync();

    Task<bool> ExistsAsync(string tasksetId);

    /// <summary>
    /// The taskset's tasks sorted by OrderIndex (the array index IS the task's
    /// position — see CONTRACT.md). Null when the taskset doesn't exist,
    /// distinguishing "unknown set" (404) from "empty set" (200 []).
    /// </summary>
    Task<IReadOnlyList<TaskDto>?> GetTasksAsync(string tasksetId);
}
