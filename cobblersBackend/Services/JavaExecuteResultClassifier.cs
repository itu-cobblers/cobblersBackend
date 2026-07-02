
using System.Runtime.CompilerServices;
using cobblersBackend.DTOs;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public class JavaExecuteResultClassifier : IExecuteResultClassifier
{
    public ExecuteResponseDto Classify(PistonExecuteResponse response)
    {
        // successful run
        if (response.Run.Code is 0)
            return new ExecuteResponseDto(ExecuteStatus.SUCCESS, response.Run.Stdout, "");

        return new ExecuteResponseDto(ClassifyFailure(response.Run.Stderr),response.Run.Stdout,response.Run.Stderr);
    }
    private static ExecuteStatus ClassifyFailure(string stderr)
    {
        // compiler error
        if(stderr.Contains(".java:") && stderr.Contains("error:"))
            return ExecuteStatus.COMPILE_ERROR;
        
        // runtime error
        if(stderr.Contains("Exception in thread"))
            return ExecuteStatus.RUNTIME_ERROR;

        // sigkill fallback
        return ExecuteStatus.RUNTIME_ERROR;
    }
}