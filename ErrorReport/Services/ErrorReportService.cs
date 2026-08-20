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

    /// <summary>
    /// NDC の排他ロック用フラグ（スタッフID + PC番号×1000）を組み立てます。
    /// オーダー／発注の更新フラグがこの値のとき、当該 PC・スタッフがロック解除中です。
    /// </summary>
    public static int BuildMyUpdateFlag(short pcNumberId, short staffId)
        => staffId + pcNumberId * 1000;

    /// <summary>
    /// 自分がロック解除している最新のオーダーID、または発注ID（マイナス）を返します。
    /// 見つからない場合は 0 です。オーダーを優先し、無ければ発注を参照します。
    /// </summary>
    public static int GetUnlockedOrderOrPurchaseId(short pcNumberId, short staffId)
    {
        if (pcNumberId <= 0 || staffId <= 0)
            return 0;

        var myFlag = BuildMyUpdateFlag(pcNumberId, staffId);
        using var db = new DbNaranjaContext();

        var orderId = db.Orders
            .Where(o => o.UpdateFlag == myFlag)
            .OrderByDescending(o => o.Id)
            .Select(o => o.Id)
            .FirstOrDefault();
        if (orderId > 0)
            return orderId;

        var purchaseId = db.Purchases
            .Where(p => p.UpdateFlag == myFlag)
            .OrderByDescending(p => p.Id)
            .Select(p => p.Id)
            .FirstOrDefault();
        return purchaseId > 0 ? -purchaseId : 0;
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
