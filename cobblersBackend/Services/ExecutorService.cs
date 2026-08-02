using System.Diagnostics;
using System.Text.RegularExpressions;

using cobblersBackend.DTOs;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public class ExecutorService : IExecutorService
{
    private readonly IPistonClient _piston;
    private readonly IExecuteResultClassifier _classifier;
    private readonly IExecutionMetrics _metrics;

    public ExecutorService(
        IPistonClient piston,
        IExecuteResultClassifier classifier,
        IExecutionMetrics metrics)
    {
        _piston = piston;
        _classifier = classifier;
        _metrics = metrics;
    }

    public Task<ExecuteResponseDto> ExecuteAsync(string javaSource) =>
        RunAsync(() => _piston.ExecuteAsync("java", new List<PistonFile>
        {
            new() { Name = "Main", Content = javaSource }
        }));

    public Task<ExecuteResponseDto> ExecuteAsync(IReadOnlyList<FileDto> files, string entryClass) =>
        RunAsync(() => _piston.ExecuteAsync("java", ToPistonFiles(files, entryClass)));

    private async Task<ExecuteResponseDto> RunAsync(Func<Task<PistonExecuteResponse>> call)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await call();
        stopwatch.Stop();

        var classifiedResponse = _classifier.Classify(response);
        _metrics.ObserveExecutionResult(classifiedResponse.Status, stopwatch.Elapsed.TotalSeconds);

        return classifiedResponse;
    }

    // Piston Java 15.0.2 has no compile stage; its run script is
    //   mv $1 $1.java && java $1.java
    // which is JEP 330 single-file source mode. Sibling .java files are NOT
    // compiled (multi-file source launch is Java 22+). So we:
    //   1. put the entry file first
    //   2. collapse every file into one compilation unit (demote other public types)
    //   3. send the entry name WITHOUT a .java suffix
    private static List<PistonFile> ToPistonFiles(IReadOnlyList<FileDto> files, string entryClass)
    {
        var entryClassName = entryClass.EndsWith(".java", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(entryClass)
            : entryClass;
        var targetFileName = $"{entryClassName}.java";

        var ordered = files.ToList();
        var entryIndex = ordered.FindIndex(f =>
            string.Equals(f.Name, targetFileName, StringComparison.OrdinalIgnoreCase));
        if (entryIndex > 0)
        {
            var entry = ordered[entryIndex];
            ordered.RemoveAt(entryIndex);
            ordered.Insert(0, entry);
        }

        var parts = new List<string>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var content = ordered[i].Content ?? "";
            if (i > 0)
            {
                var typeName = Path.GetFileNameWithoutExtension(ordered[i].Name ?? "");
                content = DemotePublicType(content, typeName);
            }
            parts.Add(content);
        }

        return
        [
            new PistonFile
            {
                Name = entryClassName,
                Content = string.Join("\n", parts)
            }
        ];
    }

    // Java allows only one public top-level type per file. After merging, only
    // the entry class may stay public; helpers become package-private.
    private static string DemotePublicType(string content, string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return content;

        return Regex.Replace(
            content,
            $@"\bpublic\s+(class|interface|enum|record)\s+{Regex.Escape(typeName)}\b",
            "$1 " + typeName);
    }
}
