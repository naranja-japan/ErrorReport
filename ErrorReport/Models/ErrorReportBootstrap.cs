namespace Naranja.ErrorReport.Models;

public sealed class ErrorReportBootstrap
{
    public short PcNumberId { get; init; }
    public short StaffId { get; init; }
    public int UnlockedOrderOrPurchaseId { get; init; }
    public string CurrentStaffShortName { get; init; } = string.Empty;
    public IReadOnlyList<StaffLookup> Staffs { get; init; } = [];
}
