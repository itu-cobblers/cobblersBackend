using Microsoft.AspNetCore.SignalR;
using cobblersBackend.Services;

namespace cobblersBackend.Hubs;

public class ExecutorHub : Hub<IExecutorClient>
{
    private readonly IExecutorService _executorService;

    public ExecutorHub(IExecutorService executorService)
    {
        _executorService = executorService;
    }

    public async Task ExecuteCode(string code)
    {
        var output = await _executorService.ExecuteAsync(code);

        await Clients.Caller.RecieveResult(output);
    }
}