using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public sealed class SessionStoreTests
{
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
}
