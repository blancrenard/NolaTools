namespace GroomingTool2.Core
{
    /// <summary>
    /// ツールモードの種類
    /// </summary>
    public enum ToolMode
    {
        Brush,      // ブラシ
        Eraser,     // 消しゴム
        Blur,       // ぼかし
        Pinch,      // つまむ
        Spread,     // 拡散
        Mask        // マスク
    }

    /// <summary>
    /// マスク選択モードの種類
    /// </summary>
    public enum MaskSelectionMode
    {
        Click,      // クリック（島単位）
        Rectangle,  // 矩形
        Lasso       // 投げ縄
    }
}

