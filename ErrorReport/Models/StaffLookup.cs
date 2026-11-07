namespace Naranja.ErrorReport.Models;

/// <summary>送信先コンボ用のスタッフ（Api の StaffLookupDto に対応）。</summary>
public sealed class StaffLookup
{
    public short Id { get; init; }

    public string? StaffCode { get; init; }

    public string? ShortName { get; init; }
}
