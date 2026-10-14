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

    [Fact(DisplayName = "SEED_ADMIN は EMAIL と PASSWORD の両方が揃ったときだけ返る")]
    public void GetInitialAdmin_RequiresEmailAndPassword()
    {
        Assert.Null(SelfHostStartup.GetInitialAdmin(Config(new())));
        Assert.Null(SelfHostStartup.GetInitialAdmin(Config(new() { ["SEED_ADMIN_EMAIL"] = "a@example.com" })));
        Assert.Null(SelfHostStartup.GetInitialAdmin(Config(new() { ["SEED_ADMIN_PASSWORD"] = "pw" })));
    }

    [Fact(DisplayName = "SEED_ADMIN は System Admin として作られ、名前は既定値を補う")]
    public void GetInitialAdmin_BuildsSystemAdmin()
    {
        var admin = SelfHostStartup.GetInitialAdmin(Config(new()
        {
            ["SEED_ADMIN_EMAIL"] = "admin@example.com",
            ["SEED_ADMIN_PASSWORD"] = "strong-password",
        }));

        Assert.NotNull(admin);
        Assert.Equal("admin@example.com", admin!.Email);
        Assert.Equal("strong-password", admin.Password);
        Assert.True(admin.IsSystemAdmin);
        Assert.False(string.IsNullOrWhiteSpace(admin.Name)); // 既定名で補完

        var named = SelfHostStartup.GetInitialAdmin(Config(new()
        {
            ["SEED_ADMIN_EMAIL"] = "admin@example.com",
            ["SEED_ADMIN_PASSWORD"] = "strong-password",
            ["SEED_ADMIN_NAME"] = "管理者",
        }));
        Assert.Equal("管理者", named!.Name);
    }
}
