using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Configuration;

namespace TaskManagementSystem.Api.Tests.Configuration;

public class SelfHostStartupTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact(DisplayName = "RUN_MIGRATIONS_ON_STARTUP 未設定なら false")]
    public void ShouldRunMigrations_DefaultsToFalse()
    {
        Assert.False(SelfHostStartup.ShouldRunMigrationsOnStartup(Config(new())));
    }

    [Theory(DisplayName = "RUN_MIGRATIONS_ON_STARTUP は true/false を解釈する")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ShouldRunMigrations_ParsesValue(string value, bool expected)
    {
        var config = Config(new() { ["RUN_MIGRATIONS_ON_STARTUP"] = value });
        Assert.Equal(expected, SelfHostStartup.ShouldRunMigrationsOnStartup(config));
    }

    [Fact(DisplayName = "SELF_HOST_SETUP 未設定なら無効(既定 false)")]
    public void IsSetupEnabled_DefaultsToFalse()
    {
        Assert.False(SelfHostStartup.IsSetupEnabled(Config(new())));
    }

    [Theory(DisplayName = "SELF_HOST_SETUP は true のときだけ有効")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void IsSetupEnabled_ParsesValue(string value, bool expected)
    {
        var config = Config(new() { ["SELF_HOST_SETUP"] = value });
        Assert.Equal(expected, SelfHostStartup.IsSetupEnabled(config));
    }
}
