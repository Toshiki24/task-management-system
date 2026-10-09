using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// AWS Secrets Manager のシークレットを、アプリの設定(IConfiguration)として読み込む。
/// </summary>
/// <remarks>
/// <para>
/// 環境変数 <see cref="SecretIdEnvironmentVariable"/> にシークレットのID(名前またはARN)が設定されている場合のみ読み込む。
/// ローカル開発・テストでは設定しないため、これまでどおり appsettings*.json の値を使う。
/// </para>
/// <para>
/// シークレットの値は、設定キーをそのまま使ったフラットなJSONとする(値はすべて文字列)。
/// 例: <c>{"Jwt:Key": "...", "ConnectionStrings:DefaultConnection": "Host=...;Password=..."}</c>
/// 秘密情報をリポジトリ・Lambdaの環境変数・Terraformのstateに置かないための仕組み(security-review.md 5.3、8章)。
/// </para>
/// </remarks>
public static class SecretsManagerConfiguration
{
    public const string SecretIdEnvironmentVariable = "APP_SECRET_ID";

    /// <summary>Git 連携(M4)用シークレットのID。Git:Secrets:* / Git:GitHub:* 等を保持する。</summary>
    public const string GitSecretIdEnvironmentVariable = "GIT_SECRET_ID";

    public static async Task AddSecretsManagerAsync(this ConfigurationManager configuration)
    {
        var apiSecretId = Environment.GetEnvironmentVariable(SecretIdEnvironmentVariable);
        var gitSecretId = Environment.GetEnvironmentVariable(GitSecretIdEnvironmentVariable);

        // どちらも未設定(ローカル・テスト・design-time)なら AWS クライアントを作らない。
        // クライアント生成はリージョン設定を要求するため、必要なときだけ生成する。
        if (string.IsNullOrEmpty(apiSecretId) && string.IsNullOrEmpty(gitSecretId))
        {
            return;
        }

        // 認証情報とリージョンは、Lambdaの実行ロール・実行環境から自動で取得される
        using var client = new AmazonSecretsManagerClient();

        await LoadIntoConfigurationAsync(configuration, client, apiSecretId);
        // Git 連携のシークレット(M4 §15 ステップ7)。設定されていなければ読み込まない
        await LoadIntoConfigurationAsync(configuration, client, gitSecretId);
    }

    private static async Task LoadIntoConfigurationAsync(
        ConfigurationManager configuration, AmazonSecretsManagerClient client, string? secretId)
    {
        if (string.IsNullOrEmpty(secretId))
        {
            return;
        }

        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId });
        configuration.AddInMemoryCollection(Parse(response.SecretString));
    }

    /// <summary>シークレットのJSONを設定キーと値の組に変換する。</summary>
    /// <exception cref="InvalidOperationException">JSONの形式が想定と異なる場合(値は含めない)</exception>
    public static IReadOnlyDictionary<string, string?> Parse(string secretString)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(secretString);
        }
        catch (JsonException)
        {
            // 例外メッセージに秘密情報の一部が含まれないよう、元の例外は含めない
            throw new InvalidOperationException("シークレットの値がJSONではありません。");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("シークレットの値は、設定キーと値の組のJSONオブジェクトにしてください。");
            }

            var values = new Dictionary<string, string?>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"シークレットの「{property.Name}」の値は文字列にしてください。");
                }

                values[property.Name] = property.Value.GetString();
            }

            return values;
        }
    }
}
