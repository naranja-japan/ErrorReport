using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Naranja.ErrorReport.Models;
using Naranja.ErrorReport.Services;
using Naranja.Platform.Data.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace Naranja.ErrorReport;

public sealed partial class MainWindow : Window
{
    private readonly ScreenshotAttachmentService _attachmentService = new();
    private readonly ObservableCollection<AttachmentItem> _attachments = [];
    private short _workingStaffId;
    private bool _pasteInProgress;

    public MainWindow()
    {
        InitializeComponent();

        AttachmentItems.ItemsSource = _attachments;

        // KeyboardAccelerator だとホバーで「Ctrl+V」ツールチップが出るため KeyDown で処理する
        RootPanel.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler(RootPanel_KeyDown),
            handledEventsToo: true);

        Closed += (_, _) => _attachmentService.Dispose();
    }

    // ─── 初期化 ───────────────────────────────────────────

    private void RootPanel_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var staffs = ErrorReportService.GetActiveStaffs();
            StaffCombo.ItemsSource = staffs;

            var (pcNumberId, staffId) = ErrorReportService.GetCurrentPcInfo();
            _workingStaffId = staffId;

            if (staffId > 0)
                StaffCombo.SelectedValue = staffId;

            var unlockedId = ErrorReportService.GetUnlockedOrderOrPurchaseId(pcNumberId, staffId);
            if (unlockedId != 0)
                OrderIdText.Text = unlockedId.ToString();
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

    private async void RootPanel_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.V || _pasteInProgress)
            return;

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if ((ctrl & CoreVirtualKeyStates.Down) == 0)
            return;

        _pasteInProgress = true;
        try
        {
            var added = await TryAddImagesFromClipboardAsync();
            if (added > 0)
                e.Handled = true;
        }
        finally
        {
            _pasteInProgress = false;
        }
    }

    private void RootPanel_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void RootPanel_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        await AddImagesFromStorageItemsAsync(items);
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tempPath })
            return;

        _attachmentService.Remove(tempPath);

        var item = _attachments.FirstOrDefault(a => a.TempPath == tempPath);
        if (item is not null)
            _attachments.Remove(item);

        UpdateAttachmentPanelVisibility();
    }

    private void AttachmentPreview_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AttachmentItem item })
            return;

        if (!File.Exists(item.TempPath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "mspaint.exe",
            Arguments = $"\"{item.TempPath}\"",
            UseShellExecute = true
        });
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
            IReadOnlyList<string> savedPaths;

            if (_attachmentService.Count > 0)
            {
                savedPaths = await ScreenshotAttachmentService.CopyToDfsAsync(_attachmentService.Paths);
            }
            else
            {
                var screenshotPath = ErrorReportService.GenerateScreenshotPath();
                var captured = await ScreenCaptureService.CaptureAndSaveAsync(screenshotPath);
                savedPaths = captured ? [screenshotPath] : [];
            }

            var staffName = ErrorReportService.GetStaffShortName(_workingStaffId);
            var pcName = Environment.MachineName.ToLower();
            bool isErrorReport = ErrorReportRadio.IsChecked == true;

            string message;
            short targetStaffId;
            var description = DescriptionText.Text.ReplaceLineEndings("\r\n");

            if (isErrorReport)
            {
                message = $"エラー報告\r\n\r\n報告者:{staffName}\r\nPC:{pcName}\r\n\r\nエラー状況:\r\n{description}\r\n\r\n";
                targetStaffId = 1;
            }
            else
            {
                message = $"スクリーンショット送付\r\n\r\nFrom:{staffName}\r\nPC:{pcName}\r\n\r\n添付文章:\r\n{description}\r\n\r\n";
                targetStaffId = GetSelectedRecipientStaffId();
            }

            if (savedPaths.Count > 0)
            {
                message += "スクリーンショット(右クリック→[ナランハファイル(フォルダ)を開く]):\r\n";
                message += string.Join("\r\n", savedPaths);
            }

            var subject = $"{SubjectText.Text} {staffName}";
            _ = int.TryParse(OrderIdText.Text, out var orderId);

            ErrorReportService.CreateReminderMail(subject, message, targetStaffId, orderId);

            await ShowSuccessInfoAsync("エラー報告を送信しました。");
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

    // ─── 添付画像 ─────────────────────────────────────────

    private async Task<int> TryAddImagesFromClipboardAsync()
    {
        if (!_attachmentService.CanAdd)
        {
            ShowAttachmentLimitInfo();
            return 0;
        }

        try
        {
            var paths = await _attachmentService.TryAddFromClipboardAsync();
            if (paths.Count == 0)
                return 0;

            var count = await RegisterAddedPathsAsync(paths);
            if (!_attachmentService.CanAdd)
                ShowAttachmentLimitInfo();
            return count;
        }
        catch (Exception ex)
        {
            ShowError($"画像の貼り付けに失敗しました。\n{ex.Message}");
            return 0;
        }
    }

    private async Task AddImagesFromStorageItemsAsync(IReadOnlyList<IStorageItem> items)
    {
        if (!_attachmentService.CanAdd)
        {
            ShowAttachmentLimitInfo();
            return;
        }

        try
        {
            var paths = await _attachmentService.TryAddFromStorageItemsAsync(items);
            if (paths.Count == 0)
                return;

            await RegisterAddedPathsAsync(paths);
            if (!_attachmentService.CanAdd)
                ShowAttachmentLimitInfo();
        }
        catch (Exception ex)
        {
            ShowError($"画像の追加に失敗しました。\n{ex.Message}");
        }
    }

    private async Task<int> RegisterAddedPathsAsync(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            var preview = await LoadPreviewAsync(path);
            _attachments.Add(new AttachmentItem
            {
                TempPath = path,
                Index = _attachments.Count + 1,
                Preview = preview
            });
        }

        UpdateAttachmentPanelVisibility();
        return paths.Count;
    }

    private static async Task<BitmapImage> LoadPreviewAsync(string path)
    {
        var image = new BitmapImage();
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        await image.SetSourceAsync(stream);
        return image;
    }

    private void UpdateAttachmentPanelVisibility()
    {
        var hasAttachments = _attachments.Count > 0;
        AttachmentScroll.Visibility = hasAttachments
            ? Visibility.Visible
            : Visibility.Collapsed;
        PasteHintText.Visibility = hasAttachments
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ShowAttachmentLimitInfo()
    {
        StatusInfoBar.Severity = InfoBarSeverity.Warning;
        StatusInfoBar.Message = $"画像は最大 {ScreenshotAttachmentService.MaxAttachments} 枚まで添付できます。";
        StatusInfoBar.IsOpen = true;
    }

    // ─── ヘルパー ─────────────────────────────────────────

    /// <summary>スクリーンショット送付先 ComboBox から担当スタッフ ID を取得します。</summary>
    private short GetSelectedRecipientStaffId()
    {
        if (StaffCombo.SelectedValue is short id)
            return id;

        if (StaffCombo.SelectedItem is Staff staff)
            return staff.Id;

        return 1;
    }

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

    private async Task ShowSuccessInfoAsync(string message)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
        await Task.Delay(2000);
        StatusInfoBar.IsOpen = false;
    }

    private async void ShowError(string message)
    {
        await ShowDialog("エラー", message);
    }
}
