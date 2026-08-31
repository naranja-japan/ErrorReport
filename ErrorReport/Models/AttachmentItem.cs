using Microsoft.UI.Xaml.Media.Imaging;

namespace Naranja.ErrorReport.Models;

/// <summary>添付画像 1 枚分（temp ファイルとプレビュー）。</summary>
public sealed class AttachmentItem
{
    public required string TempPath { get; init; }

    public required int Index { get; init; }

    public required BitmapImage Preview { get; init; }
}
