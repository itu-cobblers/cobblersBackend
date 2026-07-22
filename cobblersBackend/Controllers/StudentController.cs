using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/students")]
public class StudentController : ControllerBase
{
    private readonly IStudentService _service;

    public StudentController(IStudentService service) => _service = service;

    [HttpPut("{studentId}")]
    public async Task<IActionResult> UpsertStudent(string studentId, [FromBody] UpsertStudentRequestDto request)
    {
        await _service.UpsertStudentAsync(studentId, request.DisplayName);
        return NoContent();
    }

}