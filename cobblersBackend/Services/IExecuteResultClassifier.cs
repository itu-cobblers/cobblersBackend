

using cobblersBackend.DTOs;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public interface IExecuteResultClassifier
{
    ExecuteResponseDto Classify(PistonExecuteResponse response);
}