using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Naranja.ErrorReport.Services;

namespace Naranja.ErrorReport;

public sealed partial class MainWindow : Window
{
    private short _workingStaffId;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ─── 初期化 ───────────────────────────────────────────

    private void RootPanel_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var staffs = ErrorReportService.GetActiveStaffs();
            StaffCombo.ItemsSource = staffs;

            var (_, staffId) = ErrorReportService.GetCurrentPcInfo();
            _workingStaffId = staffId;

            if (staffId > 0)
                StaffCombo.SelectedValue = staffId;
        }
        catch (Exception ex)
        {
            ShowError($"初期データの読み込みに失敗しました。\n{ex.Message}");
        }
    }

    // ─── UI イベント ──────────────────────────────────────

    private void ScreenshotRadio_Checked(object sender, RoutedEventArgs e)
    {
        StaffCombo.IsEnabled = true;
        SubjectText.Text = "スクリーンショット送付";
        SubmitButton.Content = "スクリーンショットを送信";
    }

    private void ScreenshotRadio_Unchecked(object sender, RoutedEventArgs e)
    {
        StaffCombo.IsEnabled = false;
        SubjectText.Text = "エラー報告";
        SubmitButton.Content = "エラー報告を送信";
    }

    private void OrderIdText_TextChanged(object sender, TextChangedEventArgs e)
    {
        OrderIdLabel.Text = OrderIdText.Text.StartsWith('-') ? "発注ID:" : "オーダーID:";
    }

    // ─── 送信処理 ─────────────────────────────────────────

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DescriptionText.Text))
        {
            await ShowDialog("入力エラー", "エラーの状況を入力してください。");
            return;
        }

        SetBusy(true);

        try
        {
            // スクリーンキャプチャ
            var screenshotPath = ErrorReportService.GenerateScreenshotPath();
            var captured = await ScreenCaptureService.CaptureAndSaveAsync(screenshotPath);

            // メッセージ組み立て
            var staffName = ErrorReportService.GetStaffShortName(_workingStaffId);
            var pcName = Environment.MachineName.ToLower();
            bool isErrorReport = ErrorReportRadio.IsChecked == true;

            string message;
            short targetStaffId;
            var description = DescriptionText.Text.ReplaceLineEndings("\r\n");

            if (isErrorReport)
            {
                message = $"NDCエラー報告\r\n\r\n報告者:{staffName}\r\nPC:{pcName}\r\n\r\nエラー状況:\r\n{description}\r\n\r\n";
                targetStaffId = 1;
            }
            else
            {
                message = $"スクリーンショット送付\r\n\r\nFrom:{staffName}\r\nPC:{pcName}\r\n\r\n添付文章:\r\n{description}\r\n\r\n";
                targetStaffId = StaffCombo.SelectedValue is short id ? id : (short)1;
            }

            if (captured)
            {
                message += $"スクリーンショット(右クリック→[ナランハファイル(フォルダ)を開く]):\r\n{screenshotPath}";
            }

            var subject = $"{SubjectText.Text} {staffName}";
            _ = int.TryParse(OrderIdText.Text, out var orderId);

            ErrorReportService.CreateReminderMail(subject, message, targetStaffId, orderId);

            await ShowDialog("送信完了", "エラー報告を送信しました。");
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"送信中にエラーが発生しました。\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ─── ヘルパー ─────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        SubmitButton.IsEnabled = !busy;
        BusyRing.IsActive = busy;
        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ShowDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ShowError(string message)
    {
        await ShowDialog("エラー", message);
    }
}
