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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => "FleetTrack API is running.");
app.MapFleetTrackDefaults();
app.MapShipmentEndpoints();

app.UseHttpsRedirection();
app.Run();