using System.IO;
using System.Threading;

namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    // Keep under GitHub job timeout.
    // Override with SS14_TEST_POOL_MINUTES for long suites (maps).
    private static TimeSpan MaximumTotalTestingTimeLimit
    {
        get
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("SS14_TEST_POOL_MINUTES"), out var mins) && mins > 0)
                return TimeSpan.FromMinutes(mins);
            return TimeSpan.FromMinutes(20);
        }
    }

    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(1));

    private static bool IsCi =>
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();

        // Soft stop: ask the pool to shut down. Do not block the timer thread on this.
        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            TestContext.Error.WriteLine(
                $"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests exceeded {MaximumTotalTestingTimeLimit}. Requesting pool shutdown.\n\n");
            _ = Task.Run(() =>
            {
                try
                {
                    PoolManager.Shutdown();
                }
                catch (Exception e)
                {
                    TestContext.Error.WriteLine($"{nameof(PoolManagerTestEventHandler)}: Shutdown threw: {e}");
                }
            });
        });

        // Hard stop: NEVER call DeathReport() before Exit — it can deadlock on borrowed pairs
        // and leave the GitHub Actions step hung until job timeout ("lost communication" / cancel).
        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            TestContext.Error.WriteLine(
                $"\n\n{nameof(PoolManagerTestEventHandler)}: HARD STOP after {HardStopTimeLimit}. Forcing process exit.\n\n");

            // Best-effort report on a background thread; do not wait for it.
            _ = Task.Run(() =>
            {
                try
                {
                    var report = PoolManager.DeathReport();
                    TestContext.Error.WriteLine($"Death Report:\n{report}");
                    try
                    {
                        File.WriteAllText("pool-death-report.txt", report);
                    }
                    catch
                    {
                        // ignore
                    }
                }
                catch (Exception e)
                {
                    TestContext.Error.WriteLine($"DeathReport failed: {e}");
                }
            });

            // Give the report a brief moment, then always exit. Rely on OS `timeout` in CI as backup.
            try
            {
                Thread.Sleep(2000);
            }
            catch
            {
                // ignore
            }

            if (IsCi)
                Environment.Exit(124);

            Environment.FailFast($"Tests took way too long (>{HardStopTimeLimit}).");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            PoolManager.Shutdown();
        }
        catch
        {
            // ignore during teardown
        }
    }
}
