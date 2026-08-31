using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Naranja.ErrorReport;

/// <summary>ホバー時に手の形カーソルを出す Grid。</summary>
public sealed class HandCursorGrid : Grid
{
    public HandCursorGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
