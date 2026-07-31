using Microsoft.AspNetCore.SignalR;
using Moq;

namespace cobblersBackend.Tests.Infrastructure;

/// <summary>
/// An <see cref="IHubContext{THub}"/> that records what was broadcast to which group,
/// for services that push SignalR events as a side effect of a REST call.
///
/// Broadcasts are captured at <see cref="IClientProxy.SendCoreAsync"/> — <c>SendAsync</c>
/// is an extension method over it, so that's the only seam a mock can observe.
/// </summary>
public sealed class RecordingHubContext<THub> where THub : Hub
{
    public sealed record Broadcast(string Group, string Method, object?[] Args);

    private readonly List<Broadcast> _sent = [];

    /// <summary>Every broadcast in order.</summary>
    public IReadOnlyList<Broadcast> Sent => _sent;

    /// <summary>The hub context to hand to the service under test.</summary>
    public IHubContext<THub> Object { get; }

    /// <summary>Set to make the next send throw, for asserting best-effort behaviour.</summary>
    public Exception? ThrowOnSend { get; set; }

    public RecordingHubContext()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        string? lastGroup = null;

        clients.Setup(c => c.Group(It.IsAny<string>()))
               .Callback<string>(group => lastGroup = group)
               .Returns(proxy.Object);

        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
             .Returns<string, object?[], CancellationToken>((method, args, _) =>
             {
                 if (ThrowOnSend is not null) throw ThrowOnSend;
                 _sent.Add(new Broadcast(lastGroup ?? "<no group>", method, args));
                 return Task.CompletedTask;
             });

        var hub = new Mock<IHubContext<THub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        Object = hub.Object;
    }

    /// <summary>The single broadcast recorded, failing the test if there wasn't exactly one.</summary>
    public Broadcast Single() => Assert.Single(_sent);
}
