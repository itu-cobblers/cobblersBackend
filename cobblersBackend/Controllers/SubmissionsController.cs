using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissions;
    public SubmissionsController(ISubmissionService submissions) => _submissions = submissions;

    [HttpGet("{subId:guid}")]
    public async Task<IActionResult> GetOne(Guid subId)
    {
        var detail = await _submissions.GetSubmissionAsync(subId);
        return detail is null ? NotFound() : Ok(detail);
    }
}