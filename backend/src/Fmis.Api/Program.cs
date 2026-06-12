using Fmis.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.MigrateDatabase()
   .UseApiPipeline()
   .MapApiEndpoints();

app.Run();

public partial class Program;
