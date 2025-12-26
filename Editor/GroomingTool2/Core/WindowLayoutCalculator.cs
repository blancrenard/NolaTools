using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// GroomingTool2ウィンドウのレイアウト計算結果
    /// </summary>
    internal readonly struct WindowLayoutRects
    {
        /// <summary>上メニュー領域</summary>
        public readonly Rect TopMenu;
        
        /// <summary>右上メニュー領域</summary>
        public readonly Rect RightTopMenu;
        
        /// <summary>左メニュー（ツールバー）領域</summary>
        public readonly Rect LeftMenu;
        
        /// <summary>キャンバス領域</summary>
        public readonly Rect Canvas;
        
        /// <summary>右メニュー領域</summary>
        public readonly Rect RightMenu;

        public WindowLayoutRects(Rect topMenu, Rect rightTopMenu, Rect leftMenu, Rect canvas, Rect rightMenu)
        {
            TopMenu = topMenu;
            RightTopMenu = rightTopMenu;
            LeftMenu = leftMenu;
            Canvas = canvas;
            RightMenu = rightMenu;
        }
    }

    /// <summary>
    /// GroomingTool2のウィンドウレイアウト計算を担当するヘルパークラス
    /// </summary>
    internal static class WindowLayoutCalculator
    {
        /// <summary>サイドバーの幅</summary>
        public const float SidebarWidth = 96f;
        
        /// <summary>右メニューの幅</summary>
        public const float RightMenuWidth = 192f;
        
        /// <summary>上メニューの高さ</summary>
        public const float TopMenuHeight = 48f;
        
        /// <summary>パディング</summary>
        public const float Padding = 4f;

        /// <summary>
        /// ウィンドウサイズからレイアウト領域を計算
        /// </summary>
        /// <param name="windowRect">ウィンドウの領域（位置は0,0を想定）</param>
        /// <returns>計算されたレイアウト領域</returns>
        public static WindowLayoutRects Calculate(Rect windowRect)
        {
            // 列の幅
            float leftMenuWidth = SidebarWidth;
            float rightMenuWidth = RightMenuWidth;
            
            // 右メニューのx座標を先に決定（右上メニューと右メニューで共有）
            float rightMenuX = windowRect.width - rightMenuWidth - Padding;
            
            // 上メニューの幅（右メニューの左端までの幅 - パディング）
            float topMenuWidth = rightMenuX - Padding - Padding;
            
            // キャンバスの幅
            float canvasWidth = rightMenuX - Padding - leftMenuWidth - Padding - Padding;

            // 行の高さ
            float row1Height = TopMenuHeight;
            float row2Height = windowRect.height - row1Height - Padding * 3;

            // 各セルのRect計算
            // 行1
            float row1Y = Padding;
            var topMenuRect = new Rect(
                Padding,
                row1Y,
                topMenuWidth,
                row1Height
            );
            var rightTopMenuRect = new Rect(
                rightMenuX,
                row1Y,
                rightMenuWidth,
                row1Height
            );

            // 行2
            float row2Y = row1Y + row1Height + Padding;
            var leftMenuRect = new Rect(
                Padding,
                row2Y,
                leftMenuWidth,
                row2Height
            );
            var canvasRect = new Rect(
                leftMenuRect.xMax + Padding,
                row2Y,
                canvasWidth,
                row2Height
            );
            var rightMenuRect = new Rect(
                rightMenuX,
                row2Y,
                rightMenuWidth,
                row2Height
            );

            return new WindowLayoutRects(
                topMenuRect,
                rightTopMenuRect,
                leftMenuRect,
                canvasRect,
                rightMenuRect
            );
        }

        /// <summary>
        /// ウィンドウサイズからレイアウト領域を計算（幅と高さ指定版）
        /// </summary>
        public static WindowLayoutRects Calculate(float width, float height)
        {
            return Calculate(new Rect(0, 0, width, height));
        }
    }
}
