using System.Net.Http.Json;
using System.Text.Json;
using Naranja.ErrorReport.Models;
using Naranja.Platform.Common;

namespace Naranja.ErrorReport.Services;

/// <summary><c>Naranja.Api</c>（localhost:5180）へエラー報告用の問い合わせをします。</summary>
internal sealed class ErrorReportApiClient
{
    private const string BaseAddress = LocalApiHost.BaseAddress;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(BaseAddress),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<ErrorReportBootstrap> BootstrapAsync(string pcName, CancellationToken cancellationToken = default)
    {
        try
        {
            await LocalApiHost.EnsureReadyAsync(cancellationToken);
            var response = await Http.GetAsync(
                $"/api/apps/error-report/bootstrap?pcName={Uri.EscapeDataString(pcName)}",
                cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<ErrorReportBootstrap>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("エラー報告の初期データが空でした。");
            return payload;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Naranja.Api (http://localhost:5180) に接続できません。Api が起動しているか確認してください。",
                ex);
        }
    }

    public async Task<int> CreateReminderMailAsync(
        string subject,
        string body,
        short staffId,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await LocalApiHost.EnsureReadyAsync(cancellationToken);
            using var response = await Http.PostAsJsonAsync(
                "/api/db/comm/mail/reminder",
                new ReminderMailRequest
                {
                    Subject = subject,
                    Body = body,
                    StaffId = staffId,
                    OrderId = orderId
                },
                JsonOptions,
                cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<ReminderMailResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("リマインダーメールの応答が空でした。");
            return payload.MailId;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Naranja.Api (http://localhost:5180) に接続できません。Api が起動しているか確認してください。",
                ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Naranja.Api が {(int)response.StatusCode} を返しました。{detail}");
    }

    private sealed class ReminderMailRequest
    {
        public string? Subject { get; init; }
        public string? Body { get; init; }
        public short StaffId { get; init; }
        public int OrderId { get; init; }
    }

    private sealed class ReminderMailResponse
    {
        public int MailId { get; init; }
    }
}
