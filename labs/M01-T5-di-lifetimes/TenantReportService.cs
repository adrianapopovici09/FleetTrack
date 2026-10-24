namespace DiLifetimes;

// The singleton. This is the point of the task: write it yourself (lesson: captive dependencies).
// Step 1: take TenantContext in the constructor and expose which instance it holds → the captive.
// Step 2: fix it with IServiceScopeFactory so each call gets the TenantContext of its own scope.
// You may need two classes (the captive one and the fixed one) so both tests can exist side by side.
public sealed class TenantReportService
{
}
