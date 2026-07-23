var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var gateway = builder.AddProject<Projects.PlannerPro_Gateway>("gateway");

var web = builder.AddJavaScriptApp("web", "../web", "start")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
