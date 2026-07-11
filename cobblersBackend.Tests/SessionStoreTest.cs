using cobblersBackend.Models;
using cobblersBackend.Services;

namespace cobblersBackend.Tests;

public class SessionStoreTest
{
    [Fact]
    public void CreateSession_RecordsTasksetId_RetrievableByCode()
    {
        var store = new SessionStore();

        var code = store.CreateSession("day1-2026");

        Assert.True(store.Exists(code));
        Assert.Equal("day1-2026", store.GetTasksetId(code));
    }

    [Fact]
    public void GetTasksetId_UnknownCode_ReturnsNull()
    {
        var store = new SessionStore();

        Assert.False(store.Exists("ZZZZ"));
        Assert.Null(store.GetTasksetId("ZZZZ"));
    }

    [Fact]
    public void ImplicitlyCreatedRoom_ExistsButHasNoTasksetId()
    {
        var store = new SessionStore();

        // A hub join on a code nobody created materializes the room (GetOrAdd).
        store.AddStudent("WXYZ", new Student("uuid-1", "Maria"));

        Assert.True(store.Exists("WXYZ"));
        Assert.Null(store.GetTasksetId("WXYZ"));
    }
}
