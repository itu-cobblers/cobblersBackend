namespace cobblersBackend.Hubs;

public interface IExecutorClient
{
    Task ReceiveResult(string output);
}