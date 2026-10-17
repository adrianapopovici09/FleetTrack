// M01-T1 lab: which thread runs the code after an await?
// Run one case per process (dotnet run -- <1..5>), so every case starts on the main thread.
// In a single process, once one case resumes on a pool thread, Main stays on the pool.

var choice = args.Length > 0 ? args[0] : "1";
Console.WriteLine($"SynchronizationContext.Current is null: {SynchronizationContext.Current is null}");

switch (choice)
{
    case "1":
        Console.WriteLine("1  await Task.CompletedTask");
        Log("   before");
        await Task.CompletedTask;
        Log("   after ");
        break;

    case "2":
        Console.WriteLine("2  await Task.Delay(100)");
        Log("   before");
        await Task.Delay(100);
        Log("   after ");
        break;

    case "3":
        Console.WriteLine("3  await Task.Yield()");
        Log("   before");
        await Task.Yield();
        Log("   after ");
        break;

    case "4":
        Console.WriteLine("4  await Task.Delay(100).ConfigureAwait(false)");
        Log("   before");
        await Task.Delay(100).ConfigureAwait(false);
        Log("   after ");
        break;

    case "5":
        Console.WriteLine("5  two awaits back to back");
        Log("   A");
        await Task.Delay(100);
        Log("   B");
        await Task.Delay(100);
        Log("   C");
        break;
}

static void Log(string label) =>
    Console.WriteLine($"{label}: thread {Environment.CurrentManagedThreadId,2}, pool thread: {Thread.CurrentThread.IsThreadPoolThread}");
