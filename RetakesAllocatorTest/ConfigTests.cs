using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocatorTest;

public class ConfigTests : BaseTestFixture
{
    [Test]
    public void TestDefaultWeaponsValidation()
    {
        var usableWeapons = WeaponHelpers.AllWeapons;
        usableWeapons.Remove(CsItem.Glock);
        var warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                UsableWeapons = usableWeapons,
            }
        ).Validate();
        Assert.That(warnings[0],
            Is.EqualTo(
                "Glock18 in the DefaultWeapons.Terrorist.PistolRound " +
                "config is not in the UsableWeapons list."));

        var defaults =
            new Dictionary<CsTeam, Dictionary<WeaponAllocationType, CsItem>>(Configs.GetConfigData().DefaultWeapons);
        defaults[CsTeam.Terrorist] = new Dictionary<WeaponAllocationType, CsItem>(defaults[CsTeam.Terrorist]);
        defaults[CsTeam.Terrorist].Remove(WeaponAllocationType.FullBuyPrimary);
        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                DefaultWeapons = defaults
            }
        ).Validate();
        Assert.That(warnings[0], Is.EqualTo("Missing FullBuyPrimary in DefaultWeapons.Terrorist config."));

        defaults.Remove(CsTeam.CounterTerrorist);
        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                DefaultWeapons = defaults
            }
        ).Validate();
        Assert.That(warnings[0], Is.EqualTo("Missing FullBuyPrimary in DefaultWeapons.Terrorist config."));
        Assert.That(warnings[1], Is.EqualTo("Missing CounterTerrorist in DefaultWeapons config."));

        defaults[CsTeam.Terrorist][WeaponAllocationType.FullBuyPrimary] = CsItem.Kevlar;
        var error = Assert.Catch(() =>
        {
            Configs.OverrideConfigDataForTests(
                new ConfigData()
                {
                    DefaultWeapons = defaults
                }
            );
        });
        Assert.That(error?.Message,
            Is.EqualTo("Kevlar is not a valid weapon in config DefaultWeapons.Terrorist.FullBuyPrimary."));

        defaults =
            new Dictionary<CsTeam, Dictionary<WeaponAllocationType, CsItem>>(Configs.GetConfigData().DefaultWeapons);
        defaults[CsTeam.Terrorist][WeaponAllocationType.Preferred] = CsItem.AWP;
        error = Assert.Catch(() =>
        {
            Configs.OverrideConfigDataForTests(
                new ConfigData()
                {
                    DefaultWeapons = defaults
                }
            );
        });
        Assert.That(error?.Message, Is.EqualTo(
            "Preferred is not a valid default weapon allocation type for config DefaultWeapons.Terrorist."
        ));
    }

    [Test]
    public void TestShippedTemplateIsValidJsonc()
    {
        // The commented template written on first run must parse with the real
        // deserializer (comments + trailing commas) and pass validation.
        var configData = Configs.ParseConfigDataForTests(Configs.DefaultConfigTemplate);
        Assert.That(configData, Is.Not.Null);
        Assert.That(configData!.Validate(), Is.Empty);
    }

    [Test]
    public void TestLegacyConfigMigratesToNewLocation()
    {
        // Build a self-contained fake install tree so the ../../ hop stays inside temp.
        var root = Path.Combine(Path.GetTempPath(), "ra_migrate_" + Guid.NewGuid().ToString("N"));
        var moduleDir = Path.Combine(root, "addons", "counterstrikesharp", "plugins", "RetakesAllocator");
        var legacyDir = Path.Combine(moduleDir, "config");
        Directory.CreateDirectory(legacyDir);
        var legacyFile = Path.Combine(legacyDir, "config.json");
        var newFile = Path.Combine(
            root, "addons", "counterstrikesharp", "configs", "plugins", "RetakesAllocator", "config.json");

        // A tuned legacy config (with comments) we expect to survive the move untouched.
        var legacyContents = Configs.DefaultConfigTemplate.Replace("\"Retakes\"", "\"MIGRATED_MARKER\"");
        File.WriteAllText(legacyFile, legacyContents);

        try
        {
            var data = Configs.Load(moduleDir, saveAfterLoad: true);

            Assert.That(File.Exists(newFile), Is.True, "config should be moved to the new location");
            Assert.That(File.Exists(legacyFile), Is.False, "legacy file should be removed after the move");
            Assert.That(data.ChatMessagePluginName, Is.EqualTo("MIGRATED_MARKER"),
                "migrated values should be preserved");
            Assert.That(File.ReadAllText(newFile), Does.Contain("COMMON SETUPS"),
                "comments should be preserved verbatim by the move");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TestOnlyDefaultSelectionWarns()
    {
        var warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                AllowedWeaponSelectionTypes = new() { WeaponSelectionType.Default },
            }
        ).Validate();
        Assert.That(warnings, Has.Some.Contains("only contains 'Default'"));

        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                AllowedWeaponSelectionTypes = new(),
            }
        ).Validate();
        Assert.That(warnings, Has.Some.Contains("AllowedWeaponSelectionTypes is empty"));
    }
}
