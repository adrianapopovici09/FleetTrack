using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FleetTrack.IntegrationTests.Helpers;

internal sealed class FleetTrackApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FleetTrackApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FleetTrack", _connectionString);
        builder.UseEnvironment("Testing");
        base.ConfigureWebHost(builder);
    }
}