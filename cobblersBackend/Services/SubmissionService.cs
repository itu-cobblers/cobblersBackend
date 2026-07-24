using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.DTOs;
using cobblersBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

public class SubmissionService : ISubmissionService
{
    private readonly CobblersDbContext _db;
    private readonly IExecutorService _executor;
    private readonly IAssignmentGrader _grader;

    public SubmissionService(CobblersDbContext db, IExecutorService executor, IAssignmentGrader grader)
    {
        _db = db;
        _executor = executor;
        _grader = grader;
    }
    
    public async Task<SubmissionResponseDto?> SubmitAsync(int assignmentId, SubmissionRequestDto request)
    {
        var assignment = await _db.Assignment.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
        if(assignment is null)
            return null;
        
        if (!await _db.Student.AnyAsync(s => s.Id == request.StudentId))
            throw new InvalidOperationException($"No student '{request.StudentId}'");
        

        string? sessionId = null;
        if (request.SessionId is not null)
        {
            var code = SessionCode.Normalize(request.SessionId);
            var session = await _db.Session.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == code)
                ?? throw new InvalidOperationException($"No session with code '{request.SessionId}'");
            sessionId = session.SessionId;
        }

        var (result, passed) = await RunAndGradeAsync(assignment, request.Content);

        var submission = new Submission
        {
            SubId = Guid.NewGuid(),
            StudentId = request.StudentId,
            AssignmentId = assignmentId,
            SessionId = sessionId,
            ContentJson = request.Content.GetRawText(),
            ResultJson = result is null ? null : JsonSerializer.Serialize(result),
            Passed = passed,
            // SubmittedAt: DB-owned left unset
        };
        _db.Submission.Add(submission);
        await _db.SaveChangesAsync();

        return new SubmissionResponseDto(
            submission.SubId, submission.Passed, result, submission.SubmittedAt);
    }

    private async Task<(ExecuteResponseDto? Result, bool? Passed)> RunAndGradeAsync(Assignment assignment, JsonElement content)
    {
        if (assignment.Kind != AssignmentKind.Code)
            return (null, null);
        
        var executed = await _executor.ExecuteAsync(content.GetString()!);

        bool? passed = assignment.GradingJson is null
            ? null
            : _grader.Grade(assignment.GradingJson, new CheckResult(
                content.GetString()!,
                executed.Stdout,
                executed.Stderr,
                executed.Status == ExecuteStatus.SUCCESS ? 0 : 1)).Passed;

        return (executed, passed);
    }

}