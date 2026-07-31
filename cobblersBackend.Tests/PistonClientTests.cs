using System.Net;
using System.Text;
using System.Text.Json;
using cobblersBackend.Models;
using cobblersBackend.Services;
using Moq;

namespace cobblersBackend.Tests;

/// <summary>
/// The only component that speaks HTTP to Piston. Everything here runs against a
/// stub <see cref="HttpMessageHandler"/> — no Piston container, no network — so
/// these cover the wire shape we send, the response we parse, and which metric
/// label each failure mode records.
/// </summary>
public sealed class PistonClientTests
{
    /// <summary>Answers every request with a canned response (or throws), and keeps the last request.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request))) { }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        public static StubHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });

        /// <summary>A Piston that takes longer to answer than the caller is willing to wait.</summary>
        public static StubHandler Slow(TimeSpan delay) =>
            new(async (_, cancellationToken) =>
            {
                // Honouring the token is what a real handler does, and it's how
                // HttpClient's timeout actually surfaces: the timeout CTS fires, the
                // in-flight send observes it, and HttpClient converts that into a
                // TaskCanceledException with a TimeoutException inside.
                await Task.Delay(delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(OkResponse, Encoding.UTF8, "application/json")
                };
            });

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respond(request, cancellationToken);
        }
    }

    private const string OkResponse = """
        {"run":{"stdout":"hi\n","stderr":"","output":"hi\n","code":0,"signal":null}}
        """;

    /// <param name="timeout">
    /// The client-side budget for a Piston call. Production doesn't configure one yet, so it
    /// inherits HttpClient's 100s default — tests pass an explicit (tiny) value both to keep
    /// the suite fast and so the timeout path is provoked for real rather than simulated.
    /// </param>
    private static PistonClient Build(HttpMessageHandler handler, IExecutionMetrics? metrics = null,
                                      TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://piston.test:2000/") };
        if (timeout is not null) client.Timeout = timeout.Value;
        return new PistonClient(client, metrics ?? Mock.Of<IExecutionMetrics>());
    }

    [Fact]
    public async Task ExecuteAsync_PostsToPistonsExecuteRoute()
    {
        var handler = StubHandler.Json(OkResponse);

        await Build(handler).ExecuteAsync("java", "class Main {}");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://piston.test:2000/api/v2/execute", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_SendsLanguageWildcardVersionAndTheCode()
    {
        var handler = StubHandler.Json(OkResponse);

        await Build(handler).ExecuteAsync("java", "class Main { }");

        var sent = JsonSerializer.Deserialize<JsonElement>(handler.LastBody!);
        Assert.Equal("java", sent.GetProperty("language").GetString());
        // "*" = whatever Java the deployed Piston has; we never pin a version.
        Assert.Equal("*", sent.GetProperty("version").GetString());
        Assert.Equal("class Main { }", sent.GetProperty("files")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysSendsExactlyOneFileNamedMainJava()
    {
        var handler = StubHandler.Json(OkResponse);

        await Build(handler).ExecuteAsync("java", "public class Person {}");

        // Pins the documented single-file limitation (CLAUDE.md, "Java-only,
        // single-class assumption"): the filename is hardcoded, so a submission
        // whose public class isn't Main will not compile. Multi-file execution
        // for the Day-3 class assignments has to change this — when it does,
        // this test is the one that should fail first.
        var files = JsonSerializer.Deserialize<JsonElement>(handler.LastBody!).GetProperty("files");
        Assert.Equal(1, files.GetArrayLength());
        Assert.Equal("Main.java", files[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ParsesTheRunStage()
    {
        var handler = StubHandler.Json("""
            {"run":{"stdout":"hi\n","stderr":"boom","output":"hi\n","code":1,"signal":"SIGKILL"}}
            """);

        var result = await Build(handler).ExecuteAsync("java", "class Main {}");

        Assert.Equal("hi\n", result.Run.Stdout);
        Assert.Equal("boom", result.Run.Stderr);
        Assert.Equal(1, result.Run.Code);
        Assert.Equal("SIGKILL", result.Run.Signal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileStageIsNullOnThisPiston()
    {
        var handler = StubHandler.Json(OkResponse);

        var result = await Build(handler).ExecuteAsync("java", "class Main {}");

        // The deployed Java runtime collapses compile+run into one stage, which
        // is why JavaExecuteResultClassifier ignores Compile entirely.
        Assert.Null(result.Compile);
    }

    [Fact]
    public async Task ExecuteAsync_HttpError_Throws()
    {
        var handler = StubHandler.Json("""{"message":"nope"}""", HttpStatusCode.InternalServerError);

        // Known gotcha: no error wrapping yet, EnsureSuccessStatusCode throws
        // straight out to the caller. Pinned so adding a wrapper is a deliberate
        // change and not an accident.
        await Assert.ThrowsAsync<HttpRequestException>(() => Build(handler).ExecuteAsync("java", "class Main {}"));
    }

    [Fact]
    public async Task ExecuteAsync_HttpError_RecordsTheHttpErrorOutcome()
    {
        var metrics = new Mock<IExecutionMetrics>();
        var handler = StubHandler.Json("{}", HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Build(handler, metrics.Object).ExecuteAsync("java", "class Main {}"));

        // A 502 is not filed as a success…
        metrics.Verify(m => m.ObservePistonDuration("success", It.IsAny<double>()), Times.Never);

        // …and is counted exactly once. It used to be counted twice: the success
        // label was observed inline before EnsureSuccessStatusCode threw into the
        // catch, so every failed call added 2 to
        // cobblers_piston_request_duration_seconds{outcome="http_error"} and the
        // Grafana error rate read double. Each outcome now has exactly one
        // recording site — this Times.Once is what keeps it that way.
        metrics.Verify(m => m.ObservePistonDuration("http_error", It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Success_RecordsTheSuccessOutcome()
    {
        var metrics = new Mock<IExecutionMetrics>();
        var handler = StubHandler.Json(OkResponse);

        await Build(handler, metrics.Object).ExecuteAsync("java", "class Main {}");

        metrics.Verify(m => m.ObservePistonDuration("success", It.Is<double>(d => d >= 0)), Times.Once);
    }

    // ── The two "timeouts", which are different things ───────────────────────
    //
    //  * RUN timeout    — Piston's. It kills the student's process and answers 200
    //                     with signal=SIGKILL. Our HTTP call completes normally; see
    //                     PistonKilledTheRun below and the classifier test in
    //                     ExecutorServiceTests.
    //  * HTTP timeout   — ours. The client gives up waiting for Piston. Only this one
    //                     reaches PistonClient's catch.
    //
    // Neither is configured yet: no client.Timeout is set in Program.cs, so the HTTP
    // budget is HttpClient's 100s default. Picking real numbers is deferred to the
    // load-test PR — 30 students share one Piston, so what queueing does to p99 decides
    // whether a given budget is generous or brutal.

    [Fact]
    public async Task ExecuteAsync_HttpTimeout_RecordsTheTimeoutOutcome()
    {
        var metrics = new Mock<IExecutionMetrics>();
        // Piston is alive and would answer 200 — it's just slower than we'll wait.
        var handler = StubHandler.Slow(TimeSpan.FromSeconds(30));

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => Build(handler, metrics.Object, timeout: TimeSpan.FromMilliseconds(100))
                      .ExecuteAsync("java", "class Main {}"));

        // Provoked by a real HttpClient.Timeout rather than a hand-thrown exception, so
        // this proves the catch actually sits in the path a timeout takes. .NET marks a
        // budget-elapsed cancellation with an inner TimeoutException, which is what
        // separates it from a caller-cancelled request.
        Assert.IsType<TimeoutException>(ex.InnerException);

        // A slow Piston is its own failure mode: not a transport error, and emphatically
        // not a success. Keeping the labels apart is what makes the Grafana panel able to
        // answer "is Piston down, or just overloaded?" during the workshop.
        metrics.Verify(m => m.ObservePistonDuration("timeout", It.IsAny<double>()), Times.Once);
        metrics.Verify(m => m.ObservePistonDuration("http_error", It.IsAny<double>()), Times.Never);
        metrics.Verify(m => m.ObservePistonDuration("success", It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PistonKilledTheRun_IsANormalResponseNotATimeout()
    {
        var metrics = new Mock<IExecutionMetrics>();
        // What Piston actually returns for `while (true) {}`: it enforces its own run
        // timeout, kills the process, and answers 200 with code=null + signal=SIGKILL.
        var handler = StubHandler.Json("""
            {"run":{"stdout":"","stderr":"","output":"","code":null,"signal":"SIGKILL"}}
            """);

        var result = await Build(handler, metrics.Object).ExecuteAsync("java", "class Main {}");

        // The student's infinite loop is Piston's problem, not our HTTP client's — this is
        // a completed request. Filing it as a timeout would hide a code bug inside an
        // infrastructure metric.
        Assert.Equal("SIGKILL", result.Run.Signal);
        Assert.Null(result.Run.Code);
        metrics.Verify(m => m.ObservePistonDuration("success", It.IsAny<double>()), Times.Once);
        metrics.Verify(m => m.ObservePistonDuration("timeout", It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyBody_ThrowsRatherThanReturningNull()
    {
        // Piston answering 200 with a literal `null` body would otherwise
        // surface as a NullReferenceException deep in the classifier.
        var handler = StubHandler.Json("null");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build(handler).ExecuteAsync("java", "class Main {}"));
        Assert.Contains("empty response", ex.Message);
    }
}
