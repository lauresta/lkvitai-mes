var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.MapHealthChecks("/health");

var portal = app.MapGroup("/api/portal/v1");

portal.MapGet("/status", () => Results.Ok(new
{
    Module = "Portal",
    Status = "Online",
    Version = "0.1.0"
}));

app.Run();
