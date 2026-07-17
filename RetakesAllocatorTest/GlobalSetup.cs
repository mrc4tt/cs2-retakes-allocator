using System.Globalization;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;
using RetakesAllocatorCore.Db;

namespace RetakesAllocatorTest;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        // Force an "en" UI culture for the test process. CounterStrikeSharp's
        // JsonStringLocalizer (>= 1.0.369) falls back to reading CoreConfig.ServerLanguage
        // when the current culture has no matching lang file, and that static is null
        // outside a running game server — so an unsupported machine locale (e.g. da-DK)
        // makes the localizer throw a NullReferenceException. Pinning to "en" matches
        // RetakesAllocator/lang/en.json and avoids that fallback path entirely.
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Mute plugin console output during tests. Log writes to Console.WriteLine, which the
        // test host captures and re-emits as build warnings (one per test that logs). Tests
        // assert on Validate()'s returned warning list, not on console output, so silencing
        // here removes the noise without weakening any assertion. Static => survives the
        // per-test Configs.Load() reloads in BaseTestFixture.
        Log.LevelOverride = LogLevel.None;

        Configs.Load(".", true);
        Queries.Migrate();
        Translator.Initialize(new JsonStringLocalizer("../../../../RetakesAllocator/lang"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Queries.Disconnect();
    }
}

public abstract class BaseTestFixture
{
    [SetUp]
    public void GlobalSetup()
    {
        Configs.Load(".");
        Queries.Wipe();
    }
}