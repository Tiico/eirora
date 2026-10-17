using Eirora.Contracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/status", () => new ServerStatus { ApiVersion = ServerStatus.CurrentApiVersion });

app.Run();

/// <summary>Entry point; public so integration tests can host the server in memory.</summary>
public partial class Program;
