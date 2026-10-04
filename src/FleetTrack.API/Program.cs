using FleetTrack.API.Extensions;
using FleetTrack.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFleetTrackDefaults();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/", () => "FleetTrack API is running.");
app.MapFleetTrackDefaults();

app.UseHttpsRedirection();
app.Run();