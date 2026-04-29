using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Naranja.ErrorReport.Services;

public static class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// 全画面（仮想スクリーン）のスクリーンショットを PNG で保存します。
    /// ユーザー選択ダイアログは表示しません。
    /// </summary>
    public static Task<bool> CaptureAndSaveAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            using var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            bitmap.Save(filePath, ImageFormat.Png);

            return true;
        });
    }
}
