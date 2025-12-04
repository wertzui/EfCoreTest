var builder = DistributedApplication.CreateBuilder(args);
var password = builder.AddParameter("dbPassword", "Sup3rS3cret!");
var sqlServer = builder.AddSqlServer("sqlserver", password: password, port: 5432)
    .WithLifetime(ContainerLifetime.Persistent);
var database = sqlServer.AddDatabase("TestDatabase");

builder.AddProject<Projects.EfCoreTest>("efcoretest")
    .WithReference(database)
    .WaitFor(database)
    .WithExplicitStart();

builder.Build().Run();
