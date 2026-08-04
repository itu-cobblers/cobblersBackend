using cobblersBackend.Models;
using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public sealed class SessionStoreTests
{
    private static StudentDto Maria => new("student-maria", "Maria");
    private static StudentDto Jonas => new("student-jonas", "Jonas");

    [Fact]
    public void GetFocusedAssignment_UnknownRoom_ReturnsNull()
    {
        var store = new SessionStore();
        Assert.Null(store.GetFocusedAssignment("ABCD"));
    }

    [Fact]
    public void SetFocusedAssignment_ThenGet_ReturnsIt()
    {
        var store = new SessionStore();
        store.SetFocusedAssignment("ABCD", 101);
        Assert.Equal(101, store.GetFocusedAssignment("ABCD"));
    }

    [Fact]
    public void SetFocusedAssignment_Overwrites_KeepsOnlyLatest()
    {
        var store = new SessionStore();
        store.SetFocusedAssignment("ABCD", 101);
        store.SetFocusedAssignment("ABCD", 202);
        Assert.Equal(202, store.GetFocusedAssignment("ABCD"));
    }

    [Fact]
    public void SetFocusedAssignment_DoesNotAffectOtherRooms()
    {
        var store = new SessionStore();
        store.SetFocusedAssignment("ABCD", 101);
        Assert.Null(store.GetFocusedAssignment("WXYZ"));
    }

    // ── The live roster: what ObserveSession returns and RosterUpdated carries ──
    // This is the "who's connected right now" half of the teacher dashboard —
    // the green dots. It is deliberately NOT the persisted Attendance roll.

    [Fact]
    public void GetRoster_UnknownRoom_ReturnsEmptyNotNull()
    {
        var store = new SessionStore();

        // A teacher may observe a room before any student has joined.
        Assert.Empty(store.GetRoster("ABCD"));
    }

    [Fact]
    public void AddStudent_ReturnsRosterIncludingTheNewJoiner()
    {
        var store = new SessionStore();

        var roster = store.AddStudent("ABCD", Maria);

        // The return value is what gets broadcast as RosterUpdated, so it has to
        // already contain the student who triggered it.
        Assert.Equal(["student-maria"], roster.Select(s => s.StudentId).ToArray());
    }

    [Fact]
    public void AddStudent_SameStudentTwice_MergesByStudentId()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);

        // Reconnect with a new display name — same identity.
        var roster = store.AddStudent("ABCD", new StudentDto("student-maria", "Maria B"));

        // CONTRACT.md: "studentId keys them so duplicates merge" — a refresh must
        // not show the class twice.
        Assert.Single(roster);
        Assert.Equal("Maria B", roster[0].DisplayName);
    }

    [Fact]
    public void RemoveStudent_ShrinksTheLiveRoster()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.AddStudent("ABCD", Jonas);

        var roster = store.RemoveStudent("ABCD", "student-maria");

        Assert.NotNull(roster);
        Assert.Equal(["student-jonas"], roster.Select(s => s.StudentId).ToArray());
    }

    [Fact]
    public void RemoveStudent_UnknownRoom_ReturnsNull()
    {
        var store = new SessionStore();

        // OnDisconnectedAsync relies on this: null means "no room, nothing to
        // broadcast", as opposed to an empty roster which must still be sent.
        Assert.Null(store.RemoveStudent("ABCD", "student-maria"));
    }

    [Fact]
    public void RemoveStudent_LastOne_ReturnsEmptyRosterNotNull()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);

        var roster = store.RemoveStudent("ABCD", "student-maria");

        // The teacher must be told the room emptied — that's an empty broadcast,
        // not a skipped one.
        Assert.NotNull(roster);
        Assert.Empty(roster);
    }

    [Fact]
    public void RemoveStudent_NotInRoom_IsANoOp()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);

        var roster = store.RemoveStudent("ABCD", "student-nobody");

        Assert.NotNull(roster);
        Assert.Single(roster);
    }

    [Fact]
    public void Rooms_AreIsolated()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.AddStudent("WXYZ", Jonas);

        Assert.Equal(["student-maria"], store.GetRoster("ABCD").Select(s => s.StudentId).ToArray());
        Assert.Equal(["student-jonas"], store.GetRoster("WXYZ").Select(s => s.StudentId).ToArray());
    }

    // ── The timer ────────────────────────────────────────────────────────────

    [Fact]
    public void GetTimer_UnknownRoom_ReturnsNull()
    {
        var store = new SessionStore();
        Assert.Null(store.GetTimer("ABCD"));
    }

    [Fact]
    public void SetTimer_ThenGet_ReturnsIt()
    {
        var store = new SessionStore();
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T14:30:00Z"));

        // A late joiner's SessionState reply reads this, so it must survive
        // between the teacher's POST /timer and the student's JoinSession.
        Assert.Equal("2026-06-19T14:30:00Z", store.GetTimer("ABCD")?.EndsAt);
    }

    [Fact]
    public void SetTimer_Overwrites_KeepsOnlyLatest()
    {
        var store = new SessionStore();
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T14:30:00Z"));
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T15:00:00Z"));

        Assert.Equal("2026-06-19T15:00:00Z", store.GetTimer("ABCD")?.EndsAt);
    }

    [Fact]
    public void SetTimer_CreatesTheRoom_WithoutAnyStudents()
    {
        var store = new SessionStore();

        // The teacher can start a timer before anyone joins — GetOrAdd must not
        // trip over a missing room.
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T14:30:00Z"));

        Assert.Empty(store.GetRoster("ABCD"));
        Assert.NotNull(store.GetTimer("ABCD"));
    }

    // ── RemoveRoom: the end-session teardown ─────────────────────────────────

    [Fact]
    public void RemoveRoom_DropsRosterTimerAndFocusTogether()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.SetTimer("ABCD", new TimerInfo("2026-06-19T14:30:00Z"));
        store.SetFocusedAssignment("ABCD", 101);

        store.RemoveRoom("ABCD");

        // SessionsController.EndSession calls this — all three pieces of live
        // state have to go, or a recycled code would inherit a stale timer.
        Assert.Empty(store.GetRoster("ABCD"));
        Assert.Null(store.GetTimer("ABCD"));
        Assert.Null(store.GetFocusedAssignment("ABCD"));
    }

    [Fact]
    public void RemoveRoom_UnknownRoom_DoesNotThrow()
    {
        var store = new SessionStore();

        // EndSession on a room nobody ever joined never created a RoomState.
        store.RemoveRoom("ABCD");
    }

    [Fact]
    public void RemoveRoom_LeavesOtherRoomsAlone()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.AddStudent("WXYZ", Jonas);

        store.RemoveRoom("ABCD");

        Assert.Single(store.GetRoster("WXYZ"));
    }

    [Fact]
    public async Task AddStudent_IsSafeUnderConcurrentJoins()
    {
        var store = new SessionStore();

        // Thirty students hitting one room at once is the actual classroom
        // scenario, and the store is a singleton shared by every connection.
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            Task.Run(() => store.AddStudent("ABCD", new StudentDto($"student-{i}", $"Student {i}")))));

        Assert.Equal(30, store.GetRoster("ABCD").Count);
    }

    // ── Raise hand: the live queue, oldest-raised first ─────────────────────

    [Fact]
    public void GetRaisedHands_UnknownRoom_ReturnsEmptyNotNull()
    {
        var store = new SessionStore();
        Assert.Empty(store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RaiseHand_ThenGet_ReturnsIt()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");

        Assert.Equal(["student-maria"], store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RaiseHand_ReturnValue_MatchesGetRaisedHands()
    {
        var store = new SessionStore();

        // The hub broadcasts this return value directly — it has to match what
        // a later GetRaisedHands (e.g. a late joiner's SessionState) would see.
        var order = store.RaiseHand("ABCD", "student-maria");

        Assert.Equal(store.GetRaisedHands("ABCD"), order);
    }

    [Fact]
    public void RaiseHand_MultipleStudents_OrderedOldestRaisedFirst()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");
        store.RaiseHand("ABCD", "student-jonas");

        Assert.Equal(["student-maria", "student-jonas"], store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RaiseHand_AlreadyRaised_DoesNotBumpItsPlaceInTheQueue()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");
        store.RaiseHand("ABCD", "student-jonas");

        // Maria re-clicking the button must not jump her ahead of Jonas, who
        // was already waiting.
        store.RaiseHand("ABCD", "student-maria");

        Assert.Equal(["student-maria", "student-jonas"], store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void LowerHand_RemovesFromTheQueue()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");
        store.RaiseHand("ABCD", "student-jonas");

        var order = store.LowerHand("ABCD", "student-maria");

        Assert.Equal(["student-jonas"], order);
        Assert.Equal(["student-jonas"], store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void LowerHand_UnknownRoom_ReturnsEmptyNotNull()
    {
        var store = new SessionStore();
        Assert.Empty(store.LowerHand("ABCD", "student-maria"));
    }

    [Fact]
    public void LowerHand_StudentNotRaised_IsANoOp()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-jonas");

        var order = store.LowerHand("ABCD", "student-maria");

        Assert.Equal(["student-jonas"], order);
    }

    [Fact]
    public void RemoveStudent_AlsoLowersTheirRaisedHand()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.RaiseHand("ABCD", "student-maria");

        store.RemoveStudent("ABCD", "student-maria");

        // A disconnecting student's hand must not linger for the teacher to
        // lower manually.
        Assert.Empty(store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RemoveStudent_DoesNotAffectOtherStudentsRaisedHands()
    {
        var store = new SessionStore();
        store.AddStudent("ABCD", Maria);
        store.AddStudent("ABCD", Jonas);
        store.RaiseHand("ABCD", "student-maria");
        store.RaiseHand("ABCD", "student-jonas");

        store.RemoveStudent("ABCD", "student-maria");

        Assert.Equal(["student-jonas"], store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RemoveRoom_DropsRaisedHandsToo()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");

        store.RemoveRoom("ABCD");

        Assert.Empty(store.GetRaisedHands("ABCD"));
    }

    [Fact]
    public void RaisedHands_AreIsolatedPerRoom()
    {
        var store = new SessionStore();
        store.RaiseHand("ABCD", "student-maria");
        store.RaiseHand("WXYZ", "student-jonas");

        Assert.Equal(["student-maria"], store.GetRaisedHands("ABCD"));
        Assert.Equal(["student-jonas"], store.GetRaisedHands("WXYZ"));
    }

    [Fact]
    public async Task RaiseHand_IsSafeUnderConcurrentRaises()
    {
        var store = new SessionStore();

        // Thirty students raising a hand at once is the real "who wants to
        // answer" moment — the dictionary underneath has to survive it.
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            Task.Run(() => store.RaiseHand("ABCD", $"student-{i}"))));

        Assert.Equal(30, store.GetRaisedHands("ABCD").Count);
    }
}
