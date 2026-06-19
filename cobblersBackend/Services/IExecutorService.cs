namespace cobblersBackend.Services;

public interface IExecutorService
{
    Task<string> ExecuteAsync(string code);
}