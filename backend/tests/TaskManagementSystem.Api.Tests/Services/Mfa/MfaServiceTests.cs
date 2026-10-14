using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Services.Mfa;
using TaskManagementSystem.Api.Tests.Infrastructure;
using TaskManagementSystem.Api.Tests.Services; // JwtTestConfiguration

namespace TaskManagementSystem.Api.Tests.Services.Mfa;

public class MfaServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly ManualTimeProvider _time = new();

    public MfaServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private MfaService CreateService(AppDbContext ctx)
    {
        var config = JwtTestConfiguration.Create();
        return new MfaService(ctx, new MfaSecretProtector(config), _time, config);
    }

    private string CodeFor(string secret) => Totp.Compute(secret, _time.GetUtcNow());

    [Fact(DisplayName = "有効化は開始→確認の2段階で、確認までは有効にならない")]
    public async Task Enroll_ThenConfirm_EnablesMfa()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);

        var begin = await service.BeginEnrollmentAsync(user.Id);
        Assert.Equal(MfaBeginResult.Success, begin.Result);
        Assert.NotNull(begin.Data);
        Assert.False((await service.GetStatusAsync(user.Id)).Enabled); // 確認前は無効

        var confirm = await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));
        Assert.Equal(MfaConfirmResult.Success, confirm.Result);
        Assert.Equal(MfaService.RecoveryCodeCount, confirm.Data!.RecoveryCodes.Count);
        Assert.True((await service.GetStatusAsync(user.Id)).Enabled);

        // シークレットは暗号化して保存され、平文は DB に残らない
        var stored = await ctx.UserMfas.AsNoTracking().SingleAsync(m => m.UserId == user.Id);
        Assert.DoesNotContain(begin.Data.Secret, stored.SecretCiphertext);
    }

    [Fact(DisplayName = "誤ったコードでは確認できない")]
    public async Task Confirm_WithWrongCode_Fails()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        await service.BeginEnrollmentAsync(user.Id);

        var confirm = await service.ConfirmEnrollmentAsync(user.Id, "000000");
        Assert.Equal(MfaConfirmResult.InvalidCode, confirm.Result);
        Assert.False((await service.GetStatusAsync(user.Id)).Enabled);
    }

    [Fact(DisplayName = "開始前の確認は NotStarted")]
    public async Task Confirm_WithoutBegin_NotStarted()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);

        var confirm = await service.ConfirmEnrollmentAsync(user.Id, "123456");
        Assert.Equal(MfaConfirmResult.NotStarted, confirm.Result);
    }

    [Fact(DisplayName = "ログイン検証は TOTP で成功し、誤りは失敗する")]
    public async Task VerifyForLogin_WithTotp()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        var begin = await service.BeginEnrollmentAsync(user.Id);
        await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));

        Assert.True(await service.VerifyForLoginAsync(user.Id, CodeFor(begin.Data.Secret)));
        Assert.False(await service.VerifyForLoginAsync(user.Id, "000000"));
    }

    [Fact(DisplayName = "リカバリコードは1回だけ使え、再利用はできない")]
    public async Task RecoveryCode_UsableOnce()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        var begin = await service.BeginEnrollmentAsync(user.Id);
        var confirm = await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));
        var recovery = confirm.Data!.RecoveryCodes[0];

        Assert.True(await service.VerifyForLoginAsync(user.Id, recovery));
        Assert.False(await service.VerifyForLoginAsync(user.Id, recovery)); // 2回目は不可
    }

    [Fact(DisplayName = "無効化はコードが必要で、成功すると設定とリカバリコードが消える")]
    public async Task Disable_RemovesMfa()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        var begin = await service.BeginEnrollmentAsync(user.Id);
        await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));

        Assert.Equal(MfaDisableResult.InvalidCode, await service.DisableAsync(user.Id, "000000"));
        Assert.Equal(MfaDisableResult.Success, await service.DisableAsync(user.Id, CodeFor(begin.Data.Secret)));

        Assert.False((await service.GetStatusAsync(user.Id)).Enabled);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.UserMfas.AnyAsync(m => m.UserId == user.Id));
        Assert.False(await assert.MfaRecoveryCodes.AnyAsync(c => c.UserId == user.Id));
    }

    [Fact(DisplayName = "リカバリコード再発行で古いコードは無効になる")]
    public async Task Regenerate_InvalidatesOldCodes()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        var begin = await service.BeginEnrollmentAsync(user.Id);
        var confirm = await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));
        var oldCode = confirm.Data!.RecoveryCodes[0];

        var regen = await service.RegenerateRecoveryCodesAsync(user.Id, CodeFor(begin.Data.Secret));
        Assert.Equal(MfaRegenerateResult.Success, regen.Result);
        Assert.Equal(MfaService.RecoveryCodeCount, regen.Data!.RecoveryCodes.Count);

        // 古いコードはもう使えない
        Assert.False(await service.VerifyForLoginAsync(user.Id, oldCode));
        // 新しいコードは使える
        Assert.True(await service.VerifyForLoginAsync(user.Id, regen.Data.RecoveryCodes[0]));
    }

    [Fact(DisplayName = "有効時の再有効化開始は AlreadyEnabled")]
    public async Task Begin_WhenEnabled_AlreadyEnabled()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = CreateService(ctx);
        var begin = await service.BeginEnrollmentAsync(user.Id);
        await service.ConfirmEnrollmentAsync(user.Id, CodeFor(begin.Data!.Secret));

        Assert.Equal(MfaBeginResult.AlreadyEnabled, (await service.BeginEnrollmentAsync(user.Id)).Result);
    }
}
