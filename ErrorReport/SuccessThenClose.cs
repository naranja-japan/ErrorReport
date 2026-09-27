using Microsoft.UI.Xaml;

namespace Naranja.ErrorReport;

/// <summary>
/// モーダル等の成功後に残秒表示してからウィンドウを閉じる。
/// 通常の NdcSnackbar（窓は残す）とは別契約。Close は本ヘルパーが予約し、Snackbar には埋め込まない。
/// DataCore Features/Ui/SuccessThenClose と同契約。見た目・契約は揃える。
/// </summary>
public static class SuccessThenClose
{
    public const int DefaultDurationMs = 3000;

    /// <summary>
    /// `{指定文言}残 N 秒で閉じます。` を出し、残秒 0 で Owner を Close する。
    /// 帯に ×／キャンセルは付けない。早く閉じるときはウィンドウ自体を閉じる。
    /// </summary>
    public static async Task ShowSuccessThenCloseAsync(
        Window owner,
        NdcSnackbar snackbar,
        string message,
        int durationMs = DefaultDurationMs)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(snackbar);

        using var cts = new CancellationTokenSource();
        void OnClosed(object sender, WindowEventArgs args) => cts.Cancel();
        owner.Closed += OnClosed;

        try
        {
            var remainingMs = Math.Max(0, durationMs);
            if (remainingMs == 0)
            {
                owner.Close();
                return;
            }

            while (remainingMs > 0)
            {
                cts.Token.ThrowIfCancellationRequested();

                var seconds = (int)Math.Ceiling(remainingMs / 1000.0);
                snackbar.PresentThenCloseMessage($"{message}残 {seconds} 秒で閉じます。");

                var stepMs = Math.Min(1000, remainingMs);
                await Task.Delay(stepMs, cts.Token).ConfigureAwait(true);
                remainingMs -= stepMs;
            }

            cts.Token.ThrowIfCancellationRequested();
            owner.Close();
        }
        catch (OperationCanceledException)
        {
            // 早期 Close / タイムアウト Close とも Window.Closed で Delay をキャンセルする。
            // 業務成功は巻き戻さない。
        }
        finally
        {
            owner.Closed -= OnClosed;
        }
    }
}
