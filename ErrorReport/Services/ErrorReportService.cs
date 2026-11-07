using Naranja.ErrorReport.Models;

namespace Naranja.ErrorReport.Services;

public static class ErrorReportService
{
    private const string DfsRoot = @"\\naranja.local\dfs02";
    private static readonly ErrorReportApiClient Api = new();

    /// <summary>エラー報告画面の初期データ（スタッフ一覧・PC・ロック解除中の伝票）を Api から取得します。</summary>
    public static Task<ErrorReportBootstrap> BootstrapAsync(string? pcName = null)
        => Api.BootstrapAsync((pcName ?? Environment.MachineName).ToUpperInvariant());

    /// <summary>
    /// スクリーンショットの保存先パスを生成します。
    /// フォルダ名は現行 NDC と共有する物理パスのため変更しません。
    /// </summary>
    /// <param name="batchTime">同一送信バッチの基準時刻。省略時は現在時刻。</param>
    /// <param name="index">0 始まりの連番。0 はサフィックスなし、1 以降は _01, _02 …</param>
    public static string GenerateScreenshotPath(DateTime? batchTime = null, int index = 0)
    {
        var timestamp = (batchTime ?? DateTime.Now).ToString("yyyyMMddHHmmss");
        var suffix = index > 0 ? $"_{index:D2}" : string.Empty;
        var fileName = $"{timestamp}_{Environment.MachineName.ToLower()}{suffix}.png";
        return Path.Combine(DfsRoot, @"naranja\9000_NDC\Data\NDCエラー報告キャプチャ", fileName);
    }

    /// <summary>リマインダーメールを Api 経由で作成し、挿入された EMailID を返します。</summary>
    public static Task<int> CreateReminderMailAsync(
        string subject,
        string body,
        short staffId,
        int orderId = 0)
        => Api.CreateReminderMailAsync(subject, body, staffId, orderId);
}
