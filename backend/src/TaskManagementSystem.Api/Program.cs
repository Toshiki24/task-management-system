using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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

const string FrontendCorsPolicy = "FrontendCorsPolicy";
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(corsAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IProjectMemberService, ProjectMemberService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICommentService, CommentService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"] ?? string.Empty)),
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

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

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
