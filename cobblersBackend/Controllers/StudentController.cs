using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/students")]
public class StudentController : ControllerBase
{
    private readonly IStudentService _student;
    private readonly ISubmissionService _submission;

    public StudentController(IStudentService student, ISubmissionService submission)  
    {
        _student = student;
        _submission = submission;

    }

    [HttpPut("{studentId}")]
    public async Task<IActionResult> UpsertStudent(string studentId, [FromBody] UpsertStudentRequestDto request)
    {
        await _student.UpsertStudentAsync(studentId, request.DisplayName);
        return NoContent();
    }

    [HttpGet("{studentId}/submissions")]
    public async Task<IActionResult> GetHistory(string studentId) =>
        Ok(await _submission.GetHistoryAsync(studentId));

}