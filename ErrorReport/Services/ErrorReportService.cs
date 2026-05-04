using Naranja.Platform.Data.Base;
using Naranja.Platform.Data.Models;
using Naranja.Platform.Data.Services;

namespace Naranja.ErrorReport.Services;

public static class ErrorReportService
{
    private const string DfsRoot = @"\\naranja.local\dfs02";

    /// <summary>アクティブなスタッフ一覧を取得します。</summary>
    public static List<Staff> GetActiveStaffs()
    {
        using var db = new DbNaranjaContext();
        return [.. db.Staffs
            .Where(s => s.Id > 0 && s.Selectable)
            .OrderBy(s => s.StaffCode)];
    }

    /// <summary>現在の PC 名からスタッフ情報を特定します。</summary>
    public static (short PcNumberId, short StaffId) GetCurrentPcInfo()
    {
        var pcName = Environment.MachineName.ToUpper();
        using var db = new DbNaranjaContext();
        var pc = db.PcNumbers.FirstOrDefault(p => p.PcName == pcName);
        return pc is null ? ((short)0, (short)0) : (pc.Id, pc.StaffId);
    }

    /// <summary>スタッフの略称を取得します。</summary>
    public static string GetStaffShortName(short staffId)
    {
        using var db = new DbNaranjaContext();
        return db.Staffs
            .Where(s => s.Id == staffId)
            .Select(s => s.ShortName)
            .FirstOrDefault() ?? string.Empty;
    }

    /// <summary>スクリーンショットの保存先パスを生成します。</summary>
    public static string GenerateScreenshotPath()
    {
        var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Environment.MachineName.ToLower()}.png";
        return Path.Combine(DfsRoot, @"naranja\9000_NDC\Data\NDCエラー報告キャプチャ", fileName);
    }

    /// <summary>
    /// リマインダーメールを作成し、挿入された EMailID を返します。
    /// Naranja.Platform.Data.Services.MailService に委譲します。
    /// </summary>
    public static int CreateReminderMail(
        string subject,
        string body,
        short staffId,
        int orderId = 0)
        => MailService.CreateReminderMail(subject, body, staffId, orderId);
}
