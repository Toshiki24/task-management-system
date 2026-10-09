using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskManagementSystem.Api.Configuration;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 本番(AWS Lambda)では、署名鍵・接続文字列などの秘密情報を Secrets Manager から読み込む。
// 以降の処理(JwtSigningKey.Create 等)で使うため、設定を参照する前に読み込む
await builder.Configuration.AddSecretsManagerAsync();

// マイグレーション用 Lambda(MIGRATION_MODE=true)では、APIホストの代わりにマイグレーション処理を実行して終了する
// (同じコンテナイメージを使う。aws-architecture.md 4.4)
if (MigrationBootstrap.IsMigrationMode)
{
    await MigrationBootstrap.RunAsync(builder.Configuration);
    return;
}

// 期限通知ジョブ(JOB_MODE=due-notifications)では、APIホストの代わりにジョブを実行して終了する
// (同じコンテナイメージを使う。EventBridge のスケジュールから起動する。M3 §7)
if (DueNotificationBootstrap.IsDueNotificationJob)
{
    await DueNotificationBootstrap.RunAsync(builder.Configuration);
    return;
}

// AWS Lambda の実行環境(環境変数 AWS_LAMBDA_FUNCTION_NAME がある場合)で動いているか
var runningOnLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

// 使用しているWebサーバー(Server: Kestrel)を外部に知らせない(security-review.md 5.5)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // DateTime は常に UTC(末尾 Z)で返す。フロントでローカル時刻として誤解釈されるのを防ぐ
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });

// API Gateway(HTTP API)経由の Lambda で動かす。Lambda 以外(ローカル・テスト)では何もせず、通常どおり Kestrel で動く
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWTを 'Bearer {token}' の形式で指定してください。",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// ブラウザはBFF(Next.js)とのみ通信し、APIを直接呼び出さない(security-review.md 5.3)。
// そのためCORSは設定せず、他のオリジンのブラウザからのAPI呼び出しは許可しない。

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
// ログイン失敗回数はアプリ全体で共有するため、シングルトンで保持する
builder.Services.AddSingleton<ILoginAttemptLimiter, LoginAttemptLimiter>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IProjectMemberService, ProjectMemberService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IWorkspaceMemberService, WorkspaceMemberService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<ISystemAdminService, SystemAdminService>();
// メール送信は M1 ではプレースホルダ(ログ出力)。SES/SMTP 実装への差し替えを想定
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IWorkflowStateService, WorkflowStateService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<ISavedViewService, SavedViewService>();
builder.Services.AddScoped<IChecklistService, ChecklistService>();
builder.Services.AddScoped<IDependencyService, DependencyService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IWatcherService, WatcherService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDueNotificationService, DueNotificationService>();
builder.Services.AddScoped<ICycleService, CycleService>();
builder.Services.AddScoped<IMilestoneService, MilestoneService>();
builder.Services.AddScoped<IGitConnectionService, GitConnectionService>();
builder.Services.AddScoped<IRepositoryLinkService, RepositoryLinkService>();
builder.Services.AddScoped<IGitIdentityService, GitIdentityService>();
builder.Services.AddScoped<ITransitionRuleService, TransitionRuleService>();
builder.Services.AddScoped<IGitLinkService, GitLinkService>();
builder.Services.AddScoped<IGitActionService, GitActionService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<ITaskExportService, TaskExportService>();
builder.Services.AddScoped<ITaskImportService, TaskImportService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<ICommentService, CommentService>();

// Git プロバイダ(M4)。本番は実アダプタ(GitHub/GitLab)、本番以外はネットワークに出ない
// FakeGitProvider を使う(ローカル・E2E はこちら)。self-managed GitLab はベース URL で切り替える(§15 ステップ8)。
if (builder.Environment.IsProduction())
{
    builder.Services.AddSingleton<TaskManagementSystem.Api.Services.Git.IGitProvider,
        TaskManagementSystem.Api.Services.Git.GitHubProvider>();
    builder.Services.AddSingleton<TaskManagementSystem.Api.Services.Git.IGitProvider,
        TaskManagementSystem.Api.Services.Git.GitLabProvider>();
}
else
{
    builder.Services.AddSingleton<TaskManagementSystem.Api.Services.Git.IGitProvider>(
        _ => new TaskManagementSystem.Api.Services.Git.FakeGitProvider(TaskManagementSystem.Api.Models.GitProvider.GitHub));
    builder.Services.AddSingleton<TaskManagementSystem.Api.Services.Git.IGitProvider>(
        _ => new TaskManagementSystem.Api.Services.Git.FakeGitProvider(TaskManagementSystem.Api.Models.GitProvider.GitLab));
}
builder.Services.AddSingleton<
    TaskManagementSystem.Api.Services.Git.IGitProviderResolver,
    TaskManagementSystem.Api.Services.Git.GitProviderResolver>();

// シークレットストア(M4 §4)。本番は Secrets Manager の値を IConfiguration へ展開して引く。
// 本番以外では参照そのものを秘密として扱う(ローカル・テストの簡便化)。
var allowRefAsSecret = !builder.Environment.IsProduction();
builder.Services.AddSingleton<TaskManagementSystem.Api.Services.Git.ISecretStore>(
    sp => new TaskManagementSystem.Api.Services.Git.ConfigSecretStore(
        sp.GetRequiredService<IConfiguration>(), allowRefAsSecret));
builder.Services.AddSingleton<
    TaskManagementSystem.Api.Services.Git.IWebhookSecretResolver,
    TaskManagementSystem.Api.Services.Git.SecretStoreWebhookSecretResolver>();
// GitHub App トークン発行(M4 §15 ステップ7)。本番で実アダプタが利用する
builder.Services.AddHttpClient();
builder.Services.AddSingleton<
    TaskManagementSystem.Api.Services.Git.IGitHubAppTokenProvider,
    TaskManagementSystem.Api.Services.Git.GitHubAppTokenProvider>();

var jwtSection = builder.Configuration.GetSection("Jwt");
// 鍵が未設定・短すぎる場合は、起動時にエラーにして気付けるようにする(security-review.md SEC-07)
var jwtSigningKey = JwtSigningKey.Create(builder.Configuration);

// BFF からの呼び出しを確認する共有シークレット(X-Origin-Verify)。
// 本番では未設定・短すぎる場合に起動時エラーにする(フェイルクローズ)。本番以外で未設定なら検証しない(security-review-2.md SEC2-01 / SEC2-02)
var originVerifySecrets = OriginVerify.ResolveSecrets(builder.Configuration, builder.Environment.IsProduction());
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // "sub"クレームがClaimTypes.NameIdentifierへ自動変換されるのを防ぎ、
        // 発行時のクレーム名(JwtRegisteredClaimNames.Sub)のまま参照できるようにする
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = jwtSigningKey,
            // サーバー間の時刻のずれとして許容する時間。既定の5分のままだと、期限切れ後も5分間トークンが使えてしまう
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // 未認証・トークン期限切れ時も既定の空ボディではなく共通エラー形式を返す
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse("認証が必要です。"));
            },
        };
    });

// ログインAPIを除き、原則として全APIを認証必須とする
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// API仕様書の共通エラーレスポンス形式に合わせてバリデーションエラーを変換する
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            // "request"はボディ全体を表すASP.NET Core内部の疑似キーであり、
            // 実際のフィールド名ではないため除外する(個別フィールドのエラーは別途含まれる)
            .Where(kvp => kvp.Value?.Errors.Count > 0 && kvp.Key != "request")
            .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                new ValidationErrorItem(
                    ToCamelCase(NormalizeFieldName(kvp.Key)),
                    // JSONパスキー($, $.startDate等)や例外由来のエラーは、.NETの内部例外メッセージ
                    // (型名等を含む)をそのまま返さず、汎用的な日本語メッセージに置き換える(内部情報の非公開)
                    kvp.Key.StartsWith("$", StringComparison.Ordinal) || e.Exception is not null
                        ? "入力値の形式が正しくありません。"
                        : e.ErrorMessage)))
            .ToList();

        var response = new ValidationErrorResponse("入力内容に誤りがあります。", errors);
        return new BadRequestObjectResult(response);
    };
});

var app = builder.Build();

// すべてのレスポンス(エラーを含む)にセキュリティヘッダーを付ける(security-review.md 5.5)。
// 例外ハンドラーはレスポンスヘッダーを消去するため、送信直前(OnStarting)に設定する
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        // JSONのみを返すAPIのため、あらゆるリソースの読み込みを禁止する。
        // 開発時のSwagger UIはスクリプトやスタイルを読み込むため対象外とする
        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        }

        return Task.CompletedTask;
    });

    await next();
});

// 未処理例外はスタックトレース等の内部情報を返さず、共通エラー形式に変換する(API仕様書§32.3)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("サーバー内部でエラーが発生しました。"));
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 本番環境では、ブラウザに以後HTTPSでのみ接続させる(HSTS。security-review.md SEC-08)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// API Gateway は HTTPS でのみ受け付けるため、Lambda では HTTPS へのリダイレクトは不要
if (!runningOnLambda)
{
    app.UseHttpsRedirection();
}

// BFF からの呼び出しであることを共有シークレットで確認する(security-review-2.md SEC2-01 / SEC2-02、aws-architecture.md 5.3)。
// シークレットが設定されている場合のみ有効化する(本番では ResolveSecrets が起動時に設定を必須化する)。
// ローテーション中は新旧2値のいずれかに一致すれば通す。
// 認証より前に実行し、BFF を経由しない直接呼び出し(ログインの総当たり等)も 403 で拒否する。
if (originVerifySecrets.Count > 0)
{
    app.Use(async (context, next) =>
    {
        // 開発時の Swagger UI と、外部プロバイダが直接叩く Git Webhook は対象外とする。
        // Webhook は BFF を経由せず署名で正当性を担保する(M4 §5)
        if (!context.Request.Path.StartsWithSegments("/swagger")
            && !context.Request.Path.StartsWithSegments("/api/git/webhooks"))
        {
            var provided = context.Request.Headers[OriginVerify.HeaderName].ToString();
            if (!OriginVerify.IsValid(provided, originVerifySecrets))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse("アクセスが拒否されました。"));
                return;
            }
        }

        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string ToCamelCase(string value) =>
    string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

// JSONデシリアライズ失敗時、ModelStateのキーは"$.startDate"のようなJSONパス形式になるため、
// 先頭の"$."を取り除いてプロパティ名だけにする
static string NormalizeFieldName(string key)
{
    if (key.StartsWith("$.", StringComparison.Ordinal))
    {
        return key[2..];
    }

    // ボディ全体が不正なJSONの場合、キーはルートを表す"$"のみになる
    return key == "$" ? "body" : key;
}
