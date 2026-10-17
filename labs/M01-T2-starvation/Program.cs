// M01-T2 lab: thread-pool starvation (sync-over-async) vs proper async.
// Run without a launch profile, in Release, so it's the Production environment with no debugger:
//   dotnet run -c Release
// Restart the app between load-test runs, so one run can't inherit the threads the previous one created.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ❌ Sync-over-async: blocks a pool thread for the whole 500 ms.
app.MapGet("/slow-sync", () =>
{
    Task.Delay(500).Wait();
    return "done";
});

// ✓ Async: the thread goes back to the pool while the delay runs.
app.MapGet("/slow-async", async () =>
{
    await Task.Delay(500);
    return "done";
});

// Cheap endpoint: during the sync run, try `curl.exe -w "%{time_total}s\n" http://localhost:5000/ping`
// and see that even work that blocks nothing has to wait in the queue.
app.MapGet("/ping", () => "pong");

app.Run("http://localhost:5000");
