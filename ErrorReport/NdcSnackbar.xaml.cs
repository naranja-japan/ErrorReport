using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Naranja.ErrorReport;

/// <summary>
/// 短い成功などをコンテンツ上端付近にかぶせる社内コントロール。
/// 正本: NaranjaDataCenter/docs/WinUi3MigrationGuide.md 7章。
/// DataCore Features/Ui/NdcSnackbar と同契約。見た目は後で揃える。
/// </summary>
public sealed partial class NdcSnackbar : UserControl
{
    public const int DefaultDurationMs = 3000;

    private CancellationTokenSource? _hideCts;

    public NdcSnackbar()
    {
        InitializeComponent();
    }

    /// <summary>文言を出して既定時間後に消す。連続呼び出しは最新で置き換える。窓は閉じない。</summary>
    public async Task ShowAsync(string message, int durationMs = DefaultDurationMs)
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        _hideCts = new CancellationTokenSource();
        var token = _hideCts.Token;

        ApplyNormalLayout();
        MessageText.Text = message;
        Visibility = Visibility.Visible;

        try
        {
            await Task.Delay(durationMs, token).ConfigureAwait(true);
            HideVisual();
        }
        catch (OperationCanceledException)
        {
            // × または次の表示で打ち切り
        }
    }

    /// <summary>
    /// ShowSuccessThenCloseAsync 用の表示のみ。× なし・自動消滅なし。
    /// Owner.Close は埋め込まない（Ui ヘルパーが予約する）。
    /// </summary>
    public void PresentThenCloseMessage(string message)
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        _hideCts = null;

        CloseButton.Visibility = Visibility.Collapsed;
        RootBorder.Padding = new Thickness(12, 8, 12, 8);
        MessageText.MaxLines = 1;
        MessageText.TextWrapping = TextWrapping.NoWrap;

        MessageText.Text = message;
        Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        _hideCts?.Cancel();
        HideVisual();
    }

    private void ApplyNormalLayout()
    {
        CloseButton.Visibility = Visibility.Visible;
        RootBorder.Padding = new Thickness(12, 8, 8, 8);
        MessageText.MaxLines = 2;
        MessageText.TextWrapping = TextWrapping.Wrap;
    }

    private void HideVisual()
    {
        Visibility = Visibility.Collapsed;
        ApplyNormalLayout();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void MessageText_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (!sender.IsTextTrimmed)
            return;

        Debug.WriteLine($"NdcSnackbar: text trimmed: {sender.Text}");
    }
}
