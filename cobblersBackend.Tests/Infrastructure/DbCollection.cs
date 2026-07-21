namespace cobblersBackend.Tests.Infrastructure;

[CollectionDefinition("db")]
public sealed class DbCollection : ICollectionFixture<PostgresFixture> { }