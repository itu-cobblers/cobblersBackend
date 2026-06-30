using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/sessions")]

public class SessionController : ControllerBase
{
    private readonly SessionService _sessionService;

    public SessionController(SessionService sessionService) => _sessionService = sessionService;

    [HttpPost]
    public IActionResult CreateSession()
    {
        var session = _sessionService.CreateSession();
        return Ok(new CreateSessionResponseDto(session.Code));
    }
}
