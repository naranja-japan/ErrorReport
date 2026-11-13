using System.Reflection;
using System.Text.Json;
using Naranja.Platform.Common;

namespace Naranja.ErrorReport.Services;

/// <summary>エラー表示用の実体版（デプロイ忘れ切り分け）。</summary>
internal static class AppVersionInfo
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(LocalApiHost.BaseAddress),
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static readonly string AppVersion =
        ReadVersion(Assembly.GetExecutingAssembly());

    private static readonly string CommonVersion =
        ReadVersion(typeof(LocalApiHost).Assembly);

    /// <summary>本文の末尾に版行を付けます。Api 版は短時間で取れなければ unreachable。</summary>
    public static async Task<string> AppendFooterAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var apiVersion = await TryGetApiVersionAsync(cancellationToken).ConfigureAwait(true);
        var footer = $"ErrorReport {AppVersion} / Common {CommonVersion} / Api {apiVersion}";
        var body = (message ?? string.Empty).TrimEnd();
        return string.IsNullOrEmpty(body)
            ? footer
            : $"{body}\n\n---\n{footer}";
    }

    private static async Task<string> TryGetApiVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(2));
            // EnsureReady は呼ばない（エラー表示中に起動副作用・Mutex 経路へ入らない）
            using var response = await Http.GetAsync("/", linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return "unreachable";

            await using var stream = await response.Content.ReadAsStreamAsync(linked.Token)
                .ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: linked.Token)
                .ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("version", out var versionProp))
            {
                var raw = versionProp.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                    return Normalize(raw);
            }

            return "unknown";
        }
        catch
        {
            return "unreachable";
        }
    }

    private static string ReadVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return Normalize(informational);

        return Normalize(assembly.GetName().Version?.ToString()) is { Length: > 0 } v
            ? v
            : "?";
    }

    /// <summary>git ハッシュ付き InformationalVersion は比較しやすいよう + 以降を落とします。</summary>
    private static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "?";

        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version.Trim();
    }
}
