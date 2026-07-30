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
    private static readonly JsonSerializerOptions ResultJsonOptions = 
        new() { PropertyNameCaseInsensitive = true };

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
        return assignment.Kind switch
        {
            AssignmentKind.Code => await GradeCodeAsync(assignment, content),
            AssignmentKind.Predict => (null, GradePredict(assignment, content)),
            // Project: no automated grader yet (CONTRACT.md / SCHEMA.md).
            _ => (null, null),
        };
    }

    private async Task<(ExecuteResponseDto? Result, bool? Passed)> GradeCodeAsync(Assignment assignment, JsonElement content)
    {
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

    public async Task<SolutionResponseDto?> GetSolutionAsync(int assignmentId)
    {
        var assignment = await _db.Assignment.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment is null)
            return null;

        if (assignment.SampleSolutionJson is null)
            return new SolutionResponseDto(null);

        var solution = JsonSerializer.Deserialize<JsonElement>(assignment.SampleSolutionJson);
        return new SolutionResponseDto(solution);
    }

    /// <summary>
    /// Grade a predict answer from GradingJson `{ "predict": { compare, expectedOutput, accept? } }`.
    /// Nothing is executed — result stays null.
    /// </summary>
    private bool? GradePredict(Assignment assignment, JsonElement content)
    {
        if (assignment.GradingJson is null) return null;

        var answer = content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? ""
            : content.GetRawText();

        return _grader.Grade(assignment.GradingJson, new CheckResult(
            answer,
            Stdout: "",
            Stderr: "",
            ExitCode: 0)).Passed;
    }

    public async Task<IReadOnlyList<SubmissionHistoryDto>> GetHistoryAsync(string studentId) =>
        await _db.Submission.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.SubmittedAt)          // newest-first, per CONTRACT
            .Select(s => new SubmissionHistoryDto(
                s.SubId, s.AssignmentId,
                s.Session != null ? s.Session.Code : null,  // null for solo
                s.Passed, s.SubmittedAt))
                .ToListAsync();


    public async Task<SubmissionDetailDto?> GetSubmissionAsync(Guid subId)
    {
        var row = await _db.Submission.AsNoTracking()
            .Where(s => s.SubId == subId)
            .Select(s => new {
                s.SubId, s.StudentId, s.AssignmentId,
                SessionCode = s.Session != null ? s.Session.Code : null,
                s.ContentJson, s.ResultJson, s.Passed, s.SubmittedAt})
            .FirstOrDefaultAsync();
        if (row is null) return null;

        return new SubmissionDetailDto(
            row.SubId, row.StudentId, row.AssignmentId, row.SessionCode,
            JsonSerializer.Deserialize<JsonElement>(row.ContentJson),
            row.ResultJson is null ? null : JsonSerializer.Deserialize<ExecuteResponseDto>(row.ResultJson, ResultJsonOptions),
            row.Passed, row.SubmittedAt);
    }

    public async Task<IReadOnlyList<AssignmentSubmissionDto>?> GetSessionSubmissionsAsync(string code)
    {
        code = SessionCode.Normalize(code);

        var session = await _db.Session.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code);
        if (session is null) return null;

        return await _db.Submission.AsNoTracking()
            .Where(s => s.SessionId == session.SessionId)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AssignmentSubmissionDto(
                s.SubId, s.StudentId, s.AssignmentId, s.Passed, s.SubmittedAt)) //not sure if it needs all these //YA
            .ToListAsync();
    }
}