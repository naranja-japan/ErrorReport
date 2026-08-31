using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Naranja.ErrorReport.Services;

/// <summary>
/// セッション単位の temp フォルダにクリップボード画像を保存し、添付一覧を管理します。
/// </summary>
public sealed class ScreenshotAttachmentService : IDisposable
{
    public const int MaxAttachments = 20;

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    public static bool IsSupportedImageFile(string fileName)
        => ImageExtensions.Contains(Path.GetExtension(fileName));

    private readonly string _sessionDirectory;
    private int _nextIndex;
    private readonly List<string> _paths = [];
    private bool _disposed;

    public ScreenshotAttachmentService()
    {
        var name = $"{DateTime.Now:yyyyMMddHHmmss}_{Environment.MachineName.ToLower()}_{Guid.NewGuid():N}";
        _sessionDirectory = Path.Combine(Path.GetTempPath(), "Naranja.ErrorReport", name);
        Directory.CreateDirectory(_sessionDirectory);
    }

    public IReadOnlyList<string> Paths => _paths;

    public bool CanAdd => _paths.Count < MaxAttachments;

    public int Count => _paths.Count;

    /// <summary>クリップボードから画像を 1 枚以上 temp に保存します。画像が無い場合は空リスト。</summary>
    public async Task<IReadOnlyList<string>> TryAddFromClipboardAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!CanAdd)
            return [];

        var content = Clipboard.GetContent();

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var path = await SaveClipboardBitmapAsync(content);
            return path is null ? [] : [path];
        }

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var items = await content.GetStorageItemsAsync();
            return await TryAddFromStorageItemsAsync(items);
        }

        return [];
    }

    /// <summary>ストレージ項目から画像ファイルを temp に保存します。</summary>
    public async Task<IReadOnlyList<string>> TryAddFromStorageItemsAsync(IReadOnlyList<IStorageItem> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var added = new List<string>();

        foreach (var item in items)
        {
            if (!CanAdd)
                break;

            if (item is not StorageFile file)
                continue;

            var ext = Path.GetExtension(file.Name);
            if (!ImageExtensions.Contains(ext))
                continue;

            var path = AllocatePath();
            using var stream = await file.OpenReadAsync();
            await SaveStreamAsPngAsync(stream, path);
            _paths.Add(path);
            added.Add(path);
        }

        return added;
    }

    public void Remove(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _paths.Remove(path);
        TryDeleteFile(path);
    }

    /// <summary>temp の添付を DFS へコピーし、保存先パス一覧を返します。</summary>
    public static async Task<IReadOnlyList<string>> CopyToDfsAsync(IReadOnlyList<string> tempPaths)
    {
        if (tempPaths.Count == 0)
            return [];

        var batchTime = DateTime.Now;
        var result = new List<string>(tempPaths.Count);

        for (var i = 0; i < tempPaths.Count; i++)
        {
            var dfsPath = ErrorReportService.GenerateScreenshotPath(batchTime, i);
            var directory = Path.GetDirectoryName(dfsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var source = tempPaths[i];
            await Task.Run(() => File.Copy(source, dfsPath, overwrite: true));
            result.Add(dfsPath);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (Directory.Exists(_sessionDirectory))
                Directory.Delete(_sessionDirectory, recursive: true);
        }
        catch
        {
            // temp 削除失敗は無視
        }
    }

    private async Task<string?> SaveClipboardBitmapAsync(DataPackageView content)
    {
        if (!CanAdd)
            return null;

        var bitmapRef = await content.GetBitmapAsync();
        using var stream = await bitmapRef.OpenReadAsync();
        var path = AllocatePath();
        await SaveStreamAsPngAsync(stream, path);
        _paths.Add(path);
        return path;
    }

    private string AllocatePath()
    {
        _nextIndex++;
        return Path.Combine(_sessionDirectory, $"{_nextIndex:D3}.png");
    }

    private static async Task SaveStreamAsPngAsync(IRandomAccessStream stream, string path)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var folderPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Invalid attachment path.");
        Directory.CreateDirectory(folderPath);

        var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
        var outputFile = await folder.CreateFileAsync(
            Path.GetFileName(path),
            CreationCollisionOption.ReplaceExisting);
        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 削除失敗は無視
        }
    }
}
