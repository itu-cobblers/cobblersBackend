using cobblersBackend.Hubs;
using Microsoft.AspNetCore.Connections;
using cobblersBackend.Models;
using cobblersBackend.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace cobblersBackend.Tests;

/// <summary>
/// The hub in isolation: a real <see cref="SessionStore"/> (it's the thing whose
/// state we assert on) plus a mocked <see cref="IAttendanceService"/> and mocked
/// SignalR plumbing. No DB and no live connection.
///
/// SignalR's <c>SendAsync</c> is an extension method over
/// <see cref="IClientProxy.SendCoreAsync"/>, so broadcasts are asserted against
/// <c>SendCoreAsync(method, args, token)</c> — that's the seam Moq can see.
/// </summary>
public sealed class SessionHubTests
{
    private sealed record Broadcast(string Method, object?[] Args);

    /// <summary>Wires a hub with mocked clients/groups/context and captures every broadcast in order.</summary>
    private static (SessionHub Hub, SessionStore Store, List<Broadcast> Sent,
                    Mock<IGroupManager> Groups, IDictionary<object, object?> Items,
                    List<string> GroupsAddressed)
        BuildHub(IAttendanceService? attendance = null, SessionStore? store = null,
                 string connectionId = "conn-1")
    {
        store ??= new SessionStore();
        var sent = new List<Broadcast>();
        var groupsAddressed = new List<string>();

        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
             .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add(new Broadcast(method, args)))
             .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Group(It.IsAny<string>()))
               .Callback<string>(groupsAddressed.Add)
               .Returns(proxy.Object);

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // The real connection bag, not a plain Dictionary: ConnectionItems'
        // indexer returns null for a missing key, while Dictionary throws.
        // OnDisconnectedAsync reads Items["code"] unguarded and relies on that
        // null for observers who never joined — a Dictionary double would fail
        // the test for a reason production never hits.
        IDictionary<object, object?> items = new ConnectionItems();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);
        context.SetupGet(c => c.Items).Returns(items);

        var hub = new SessionHub(store, attendance ?? Mock.Of<IAttendanceService>())
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object,
        };

        return (hub, store, sent, groups, items, groupsAddressed);
    }

    private static JoinArgs Join(string code = "ABCD", string studentId = "student-maria",
                                 string displayName = "Maria") => new(code, studentId, displayName);

    // ── JoinSession ──────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinSession_PersistsAttendanceBeforeTouchingLiveState()
    {
        var attendance = new Mock<IAttendanceService>();
        var (hub, store, _, _, _, _) = BuildHub(attendance.Object);

        await hub.JoinSession(Join());

        // The persisted roll is what the teacher dashboard hydrates from, so a
        // join that never reaches the DB is a join the teacher loses on reload.
        attendance.Verify(a => a.RecordAttendanceAsync("ABCD", "student-maria"), Times.Once);
        Assert.Single(store.GetRoster("ABCD"));
    }

    [Fact]
    public async Task JoinSession_NormalizesTheCode_Everywhere()
    {
        var attendance = new Mock<IAttendanceService>();
        var (hub, store, _, groups, items, groupsAddressed) = BuildHub(attendance.Object);

        await hub.JoinSession(Join(code: "  abcd "));

        // SessionCode's whole point: one normalized key for the DB lookup, the
        // group name, the store key and the saved connection state. A raw code
        // surviving into any one of them silently splits the room in two.
        attendance.Verify(a => a.RecordAttendanceAsync("ABCD", "student-maria"), Times.Once);
        groups.Verify(g => g.AddToGroupAsync("conn-1", "ABCD", It.IsAny<CancellationToken>()), Times.Once);
        Assert.All(groupsAddressed, g => Assert.Equal("ABCD", g));
        Assert.Single(store.GetRoster("ABCD"));
        Assert.Equal("ABCD", items["code"]);
    }

    [Fact]
    public async Task JoinSession_UnknownRoom_ThrowsHubExceptionCarryingTheReason()
    {
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.RecordAttendanceAsync(It.IsAny<string>(), It.IsAny<string>()))
                  .ThrowsAsync(new InvalidOperationException("No session with code 'ABCD'"));
        var (hub, _, _, _, _, _) = BuildHub(attendance.Object);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinSession(Join()));

        // HubException specifically: its message reaches the client even with
        // detailed errors disabled, so the student sees *why* the join failed.
        Assert.Contains("No session with code", ex.Message);
    }

    [Fact]
    public async Task JoinSession_UnknownRoom_LeavesNoGhostInTheRoster()
    {
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.RecordAttendanceAsync(It.IsAny<string>(), It.IsAny<string>()))
                  .ThrowsAsync(new InvalidOperationException("No session with code 'ABCD'"));
        var (hub, store, sent, groups, items, _) = BuildHub(attendance.Object);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinSession(Join()));

        // "Persistence is authoritative" — a failed join must not half-register.
        Assert.Empty(store.GetRoster("ABCD"));
        Assert.Empty(sent);
        Assert.Empty(items);
        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                      Times.Never);
    }

    [Fact]
    public async Task JoinSession_BroadcastsStudentJoinedThenRosterUpdated()
    {
        var (hub, _, sent, _, _, _) = BuildHub();

        await hub.JoinSession(Join());

        Assert.Equal(["StudentJoined", "RosterUpdated"], sent.Select(b => b.Method).ToArray());

        var joined = Assert.IsType<StudentDto>(sent[0].Args[0]);
        Assert.Equal("student-maria", joined.StudentId);
        Assert.Equal("Maria", joined.DisplayName);

        // RosterUpdated carries the full connected list — the frontend replaces
        // its live set with this, so the new joiner must already be in it.
        var roster = Assert.IsAssignableFrom<IReadOnlyList<StudentDto>>(sent[1].Args[0]);
        Assert.Equal(["student-maria"], roster.Select(s => s.StudentId).ToArray());
    }

    [Fact]
    public async Task JoinSession_RemembersConnectionStateForDisconnect()
    {
        var (hub, _, _, _, items, _) = BuildHub();

        await hub.JoinSession(Join());

        // OnDisconnectedAsync has nothing but Context.Items to work out which
        // student just dropped out of which room.
        Assert.Equal("ABCD", items["code"]);
        Assert.Equal("student-maria", items["studentId"]);
    }

    [Fact]
    public async Task JoinSession_RepliesWithTheRoomsTimerAndFocus()
    {
        var store = new SessionStore();
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T14:30:00Z"));
        store.SetFocusedAssignment("ABCD", 101);
        var (hub, _, _, _, _, _) = BuildHub(store: store);

        var state = await hub.JoinSession(Join());

        // A late joiner syncs to the countdown and the assignment the teacher is
        // on — this reply is the only place they get it.
        Assert.Equal("2026-06-19T14:30:00Z", state.ActiveTimer?.EndsAt);
        Assert.Equal(101, state.FocusedAssignmentId);
    }

    [Fact]
    public async Task JoinSession_QuietRoom_RepliesWithNulls()
    {
        var (hub, _, _, _, _, _) = BuildHub();

        var state = await hub.JoinSession(Join());

        Assert.Null(state.ActiveTimer);
        Assert.Null(state.FocusedAssignmentId);
        Assert.Empty(state.RaisedHandStudentIds);
    }

    [Fact]
    public async Task JoinSession_RepliesWithTheRaisedHandQueue()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-jonas");
        var (hub, _, _, _, _, _) = BuildHub(store: store);

        // A late joiner syncs to who's already waiting — same reasoning as the
        // timer and focused-assignment replies above.
        var state = await hub.JoinSession(Join());

        Assert.Equal(["student-jonas"], state.RaisedHandStudentIds);
    }

    [Fact]
    public async Task JoinSession_Rejoin_DoesNotDuplicateTheStudent()
    {
        var (hub, store, _, _, _, _) = BuildHub();

        await hub.JoinSession(Join());
        await hub.JoinSession(Join(displayName: "Maria on her phone"));

        // Refresh / reconnect is the common case, not the exception.
        Assert.Single(store.GetRoster("ABCD"));
        Assert.Equal("Maria on her phone", store.GetRoster("ABCD")[0].DisplayName);
    }

    // ── ObserveSession ───────────────────────────────────────────────────────

    [Fact]
    public async Task ObserveSession_ReturnsTheLiveRoster_AndJoinsTheGroup()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", new StudentDto("student-maria", "Maria"));
        var (hub, _, _, groups, _, _) = BuildHub(store: store);

        var roster = await hub.ObserveSession("abcd");

        Assert.Equal(["student-maria"], roster.Select(s => s.StudentId).ToArray());
        groups.Verify(g => g.AddToGroupAsync("conn-1", "ABCD", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObserveSession_DoesNotMakeTheTeacherAnAttendee()
    {
        var attendance = new Mock<IAttendanceService>();
        var (hub, store, sent, _, items, _) = BuildHub(attendance.Object);

        await hub.ObserveSession("ABCD");

        // Watching a room is not attending it: no Attendance row, no roster
        // entry, and nothing broadcast to the students.
        attendance.Verify(a => a.RecordAttendanceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(store.GetRoster("ABCD"));
        Assert.Empty(sent);
        Assert.Empty(items);
    }

    [Fact]
    public async Task ObserveSession_EmptyRoom_ReturnsEmptyList()
    {
        var (hub, _, _, _, _, _) = BuildHub();

        Assert.Empty(await hub.ObserveSession("ABCD"));
    }

    // ── FocusAssignment ──────────────────────────────────────────────────────

    [Fact]
    public async Task FocusAssignment_StoresItAndBroadcastsIt()
    {
        var (hub, store, sent, _, _, groupsAddressed) = BuildHub();

        await hub.FocusAssignment("abcd", 101);

        // Stored so late joiners sync (S11), broadcast so present students follow.
        Assert.Equal(101, store.GetFocusedAssignment("ABCD"));
        Assert.Equal("ABCD", Assert.Single(groupsAddressed));
        var sentEvent = Assert.Single(sent);
        Assert.Equal("AssignmentFocused", sentEvent.Method);
        Assert.Equal(101, sentEvent.Args[0]);
    }

    [Fact]
    public async Task JoinSession_AfterTeacherFocusedAssignment_RepliesWithThatFocus()
    {
        var (hub, _, sent, _, _, _) = BuildHub();

        await hub.FocusAssignment("abcd", 101);
        sent.Clear();

        // Student joins late sees FocusedAssignment
        var state = await hub.JoinSession(Join());

        Assert.Equal(101, state.FocusedAssignmentId);
    }

    // ── RaiseHand / LowerHand ────────────────────────────────────────────────

    [Fact]
    public async Task RaiseHand_StoresItAndBroadcastsIt()
    {
        var (hub, store, sent, _, _, groupsAddressed) = BuildHub();

        await hub.RaiseHand("abcd", "student-maria");

        // Stored so late joiners sync (JoinSession's raisedHandStudentIds),
        // broadcast so the teacher and the raising student's own tab update.
        Assert.Equal(["student-maria"], store.GetRaisedHands("ABCD"));
        Assert.Equal("ABCD", Assert.Single(groupsAddressed));
        var sentEvent = Assert.Single(sent);
        Assert.Equal("HandsUpdated", sentEvent.Method);
        var order = Assert.IsAssignableFrom<IReadOnlyList<string>>(sentEvent.Args[0]);
        Assert.Equal(["student-maria"], order);
    }

    [Fact]
    public async Task LowerHand_StoresItAndBroadcastsIt()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");
        var (hub, _, sent, _, _, groupsAddressed) = BuildHub(store: store);

        await hub.LowerHand("abcd", "student-maria");

        Assert.Empty(store.GetRaisedHands("ABCD"));
        Assert.Equal("ABCD", Assert.Single(groupsAddressed));
        var sentEvent = Assert.Single(sent);
        Assert.Equal("HandsUpdated", sentEvent.Method);
        var order = Assert.IsAssignableFrom<IReadOnlyList<string>>(sentEvent.Args[0]);
        Assert.Empty(order);
    }

    [Fact]
    public async Task RaiseHand_ThenLowerHand_BroadcastsTheQueueBothTimes()
    {
        var (hub, _, sent, _, _, _) = BuildHub();

        await hub.RaiseHand("ABCD", "student-maria");
        await hub.LowerHand("ABCD", "student-maria");

        // The student's own button and the teacher's roster both listen for
        // the same event on the way up and on the way down.
        Assert.Equal(["HandsUpdated", "HandsUpdated"], sent.Select(b => b.Method).ToArray());
        Assert.Equal(["student-maria"], Assert.IsAssignableFrom<IReadOnlyList<string>>(sent[0].Args[0]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(sent[1].Args[0]));
    }

    [Fact]
    public async Task JoinSession_AfterHandRaised_RepliesWithThatQueue()
    {
        var (hub, _, sent, _, _, _) = BuildHub();

        await hub.RaiseHand("abcd", "student-jonas");
        sent.Clear();

        // Same shape as the focused-assignment analog above — the hub method
        // that raises a hand needs no REST step, mirroring FocusAssignment.
        var state = await hub.JoinSession(Join());

        Assert.Equal(["student-jonas"], state.RaisedHandStudentIds);
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_DropsTheStudentAndBroadcastsTheNewRoster()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", new StudentDto("student-jonas", "Jonas"));
        var (hub, _, sent, _, _, _) = BuildHub(store: store);
        await hub.JoinSession(Join());
        sent.Clear();

        await hub.OnDisconnectedAsync(null);

        // Presence only: the dot goes gray. Nothing here deletes an Attendance
        // row — that's the whole gray-vs-green split in CONTRACT.md.
        Assert.Equal(["student-jonas"], store.GetRoster("ABCD").Select(s => s.StudentId).ToArray());
        var broadcast = Assert.Single(sent);
        Assert.Equal("RosterUpdated", broadcast.Method);
        var roster = Assert.IsAssignableFrom<IReadOnlyList<StudentDto>>(broadcast.Args[0]);
        Assert.Equal(["student-jonas"], roster.Select(s => s.StudentId).ToArray());
    }

    [Fact]
    public async Task OnDisconnectedAsync_LastStudent_BroadcastsAnEmptyRoster()
    {
        var (hub, store, sent, _, _, _) = BuildHub();
        await hub.JoinSession(Join());
        sent.Clear();

        await hub.OnDisconnectedAsync(null);

        // The teacher needs to see the room empty out, so an empty list still ships.
        Assert.Empty(store.GetRoster("ABCD"));
        var roster = Assert.IsAssignableFrom<IReadOnlyList<StudentDto>>(Assert.Single(sent).Args[0]);
        Assert.Empty(roster);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ObserverWhoNeverJoined_BroadcastsNothing()
    {
        var (hub, _, sent, _, _, _) = BuildHub();
        await hub.ObserveSession("ABCD");

        await hub.OnDisconnectedAsync(null);

        // A teacher closing their tab is not a roster change. Context.Items is
        // empty for observers, which is exactly what the guard keys on.
        Assert.Empty(sent);
    }

    [Fact]
    public async Task OnDisconnectedAsync_AfterTheRoomWasRemoved_BroadcastsNothing()
    {
        var (hub, store, sent, _, _, _) = BuildHub();
        await hub.JoinSession(Join());
        store.RemoveRoom("ABCD");   // teacher ended the session first
        sent.Clear();

        await hub.OnDisconnectedAsync(null);

        // RemoveStudent returns null for a room that's gone — the null check is
        // what stops an end-of-class disconnect storm from broadcasting.
        Assert.Empty(sent);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithAnException_StillCleansUp()
    {
        var (hub, store, _, _, _, _) = BuildHub();
        await hub.JoinSession(Join());

        // A dropped connection (network loss) arrives with a non-null exception.
        await hub.OnDisconnectedAsync(new IOException("connection reset"));

        Assert.Empty(store.GetRoster("ABCD"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_StudentWithRaisedHand_AlsoBroadcastsHandsUpdated()
    {
        var (hub, store, sent, _, _, _) = BuildHub();
        await hub.JoinSession(Join());
        await hub.RaiseHand("ABCD", "student-maria");
        sent.Clear();

        await hub.OnDisconnectedAsync(null);

        // Disconnecting must drop the stale raised hand immediately — the
        // teacher's queue can't wait on someone noticing and clicking it down.
        Assert.Empty(store.GetRaisedHands("ABCD"));
        Assert.Equal(["RosterUpdated", "HandsUpdated"], sent.Select(b => b.Method).ToArray());
        var handsOrder = Assert.IsAssignableFrom<IReadOnlyList<string>>(sent[1].Args[0]);
        Assert.Empty(handsOrder);
    }

    [Fact]
    public async Task OnDisconnectedAsync_StudentWithoutRaisedHand_DoesNotBroadcastHandsUpdated()
    {
        var (hub, _, sent, _, _, _) = BuildHub();
        await hub.JoinSession(Join());
        sent.Clear();

        await hub.OnDisconnectedAsync(null);

        // No raised hand to clear — an extra HandsUpdated on every ordinary
        // disconnect would just be broadcast noise.
        Assert.Equal(["RosterUpdated"], sent.Select(b => b.Method).ToArray());
    }

    [Fact]
    public async Task OnDisconnectedAsync_OtherStudentsRaisedHands_AreUnaffected()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", new StudentDto("student-jonas", "Jonas"));
        store.RaiseHand("ABCD", "student-jonas");
        var (hub, _, sent, _, _, _) = BuildHub(store: store);
        await hub.JoinSession(Join());
        sent.Clear();

        // Maria — who never raised her hand — disconnects.
        await hub.OnDisconnectedAsync(null);

        Assert.Equal(["RosterUpdated"], sent.Select(b => b.Method).ToArray());
        Assert.Equal(["student-jonas"], store.GetRaisedHands("ABCD"));
    }
}
