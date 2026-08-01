using cobblersBackend.DTOs;
using cobblersBackend.Data;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace cobblersBackend.Tests.Infrastructure;

/// <summary>
/// Constructs services under test with their collaborators defaulted to inert doubles,
/// so a constructor change is one edit here instead of one per call site. (Adding the
/// hub + logger for SubmissionRecorded would otherwise have touched 23 of them.)
/// </summary>
public static class TestServices
{
    public static AssignmentService Assignments(CobblersDbContext ctx) => new(ctx);

    public static AssignmentSetService AssignmentSets(CobblersDbContext ctx) =>
        new(ctx, Assignments(ctx));

    /// <param name="executor">Defaults to one that always reports a silent success.</param>
    /// <param name="hub">
    /// Pass a <see cref="RecordingHubContext{THub}"/>'s <c>Object</c> to assert on
    /// broadcasts; the default swallows them.
    /// </param>
    public static SubmissionService Submissions(
        CobblersDbContext ctx,
        IExecutorService? executor = null,
        IHubContext<SessionHub>? hub = null) =>
        new(ctx,
            executor ?? new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "", "")),
            new AssignmentGrader(),
            hub ?? new RecordingHubContext<SessionHub>().Object,
            NullLogger<SubmissionService>.Instance);
}
