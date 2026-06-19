namespace cobblersBackend.Hubs;

public interface IExecutorClient
{
    Task RecieveResult(string output);
}