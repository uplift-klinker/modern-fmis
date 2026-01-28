var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Modern_Fmis_Api_Host>("api");

builder.AddProject<Projects.Modern_Fmis_Web_Host>("web")
    .WithEndpoint(name: "web-http", port: 5107, scheme: "http")
    .WithEndpoint(name: "web-https", port: 7007, scheme: "https")
    .WithReference(api);

builder.Build().Run();
