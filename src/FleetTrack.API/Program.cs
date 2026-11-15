using FleetTrack.API.Endpoints.Shipments;
using FleetTrack.API.Extensions;
using FleetTrack.Application.Extensions;
using FleetTrack.Infrastructure.Extensions;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFleetTrackDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline. Order matters: each middleware only sees
// what runs after it, so the error handlers come first.
app.UseExceptionHandler();   // unhandled exception -> 500 Problem Details (UnhandledExceptionHandler)
app.UseStatusCodePages();    // empty 4xx/5xx (no route, bad JSON) -> Problem Details body
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => "FleetTrack API is running.");
app.MapFleetTrackDefaults();
app.MapShipmentEndpoints();

app.Run();