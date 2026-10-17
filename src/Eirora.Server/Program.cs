using Eirora.Contracts;
using Eirora.Simulation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapGet("/api/info", () => new ServerInfo(RulesVersion.Current));

app.Run();
