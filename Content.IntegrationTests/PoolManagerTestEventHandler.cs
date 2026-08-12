namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    // Keep under GitHub job timeout; FailFast kills the whole runner process (GHA: "lost communication").
    // Override with SS14_TEST_POOL_MINUTES for long suites (maps).
    private static TimeSpan MaximumTotalTestingTimeLimit
    {
        get
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("SS14_TEST_POOL_MINUTES"), out var mins) && mins > 0)
                return TimeSpan.FromMinutes(mins);
            return TimeSpan.FromMinutes(25);
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
        // If the tests seem to be stuck, we try to end it semi-nicely
        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            // This can and probably will cause server/client pairs to shut down MID test, and will lead to really confusing test failures.
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long. Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
            try
            {
                PoolManager.Shutdown();
            }
            catch (Exception e)
            {
                TestContext.Error.WriteLine($"{nameof(PoolManagerTestEventHandler)}: Shutdown threw: {e}");
            }
        });

        // If ending it nicely doesn't work within a minute, force-exit the test host.
        // On CI prefer Environment.Exit — FailFast terminates the GitHub runner agent itself.
        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            string deathReport;
            try
            {
                deathReport = PoolManager.DeathReport();
            }
            catch (Exception e)
            {
                deathReport = $"DeathReport failed: {e}";
            }

            var message = $"Tests took way too long.\n Death Report:\n{deathReport}";
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: HARD STOP: {message}\n\n");

            if (IsCi)
                Environment.Exit(1);

            Environment.FailFast(message);
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}
