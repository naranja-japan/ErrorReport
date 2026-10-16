using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Naranja.ErrorReport;

/// <summary>
/// Status InfoBar の Error 用。塗りつぶし赤 ●＋白太 ×（公式 MDL2 の線 × 円は閉じるボタンと誤認しやすい）。
/// </summary>
public static class InfoBarErrorIcon
{
    private static IconSource? _cached;

    public static IconSource Create()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "InfoBarError.png");
        _cached = new ImageIconSource
        {
            ImageSource = new BitmapImage(new Uri(path, UriKind.Absolute)),
        };
        return _cached;
    }

    public static void Apply(InfoBar infoBar, InfoBarSeverity severity)
    {
        infoBar.IconSource = severity == InfoBarSeverity.Error ? Create() : null;
    }
}
