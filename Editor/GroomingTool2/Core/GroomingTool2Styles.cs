using NolaTools;
using UnityEngine;

namespace GroomingTool2.Core
{
    internal static class GroomingTool2Styles
    {
        public static readonly GUILayoutOption LabelWidth = GUILayout.Width(75);
        public static readonly GUILayoutOption MediumLabelWidth = GUILayout.Width(65);
        public static readonly GUILayoutOption NarrowLabelWidth = GUILayout.Width(50);
        public static readonly GUILayoutOption SliderWidth = GUILayout.Width(120);
        public static readonly GUILayoutOption ToggleWidth = GUILayout.Width(100);
        public static readonly float SliderFieldWidth = 40f;
        public static readonly float Spacing = 8f;
        public static readonly float SmallSpacing = 4f;

        private static string L(string jp, string en) => NolaToolsLocalization.L(jp, en);

        // ツールチップ付きラベル
        public static GUIContent BrushSizeLabel => new GUIContent(
            L("ブラシのサイズ", "Brush Size"),
            L("ブラシの直径を設定します。Alt+マウスホイールでも調整可能です。",
              "Sets the brush diameter. Also adjustable with Alt+Mouse Wheel."));
        public static GUIContent BrushPowerLabel => new GUIContent(
            L("ブラシの強さ", "Brush Strength"),
            L("ブラシの適用強度を設定します。値が大きいほど効果が強くなります。",
              "Sets the brush strength. Higher values result in stronger effects."));
        public static GUIContent InclinedLabel => new GUIContent(
            L("毛の傾き", "Fur Tilt"),
            L("毛の傾斜角度を設定します。0で直立、1に近いほど寝かせます。",
              "Sets the fur tilt angle. 0 is upright, values near 1 lay the fur down."));
        public static GUIContent DirectionOnlyLabel => new GUIContent(
            L("向きのみ変更", "Direction Only"),
            L("有効にすると、毛の向きのみを変更し傾きは維持します。",
              "When enabled, only the fur direction is changed while tilt is preserved."));
        public static GUIContent InclinedOnlyLabel => new GUIContent(
            L("傾きのみ変更", "Tilt Only"),
            L("有効にすると、毛の傾きのみを変更し向きは維持します。",
              "When enabled, only the fur tilt is changed while direction is preserved."));
        public static GUIContent MirrorLabel => new GUIContent(
            L(" ミラー", " Mirror"),
            L("左右対称に編集します。アバターの対称部位に同時に適用されます。",
              "Edit symmetrically. Applied simultaneously to the symmetric part of the avatar."));
        public static GUIContent InvertLabel => new GUIContent(
            L("効果反転(拡散)", "Invert (Spread)"),
            L("つまむモードの効果を反転させます。",
              "Inverts the effect of Pinch mode."));
        public static GUIContent DotIntervalFormat => new GUIContent(
            L("ドット間隔: {0}", "Dot Interval: {0}"));

        // 拡大縮小のラベル
        public static GUIContent ScaleLabel => new GUIContent(
            L("拡大縮小", "Scale"),
            L("キャンバスの表示倍率を設定します。Ctrl+マウスホイールでも調整可能です。",
              "Sets the canvas display scale. Also adjustable with Ctrl+Mouse Wheel."));

        // 自動設定のツールチップ
        public static GUIContent AutoSetupSurfaceLiftLabel => new GUIContent(
            L("毛の傾き", "Fur Tilt"),
            L("自動生成される毛の傾き具合を設定します。0で直立、0.95で面に沿った向きになります。",
              "Sets the tilt of auto-generated fur. 0 is upright, 0.95 follows the surface."));
        public static GUIContent AutoSetupRandomnessLabel => new GUIContent(
            L("ランダム性", "Randomness"),
            L("毛の向きにランダムなばらつきを加えます。自然な見た目になります。",
              "Adds random variation to fur direction for a natural look."));

        // モード選択ボタン（左メニュー）
        public static GUIContent SceneEditingLabel => new GUIContent(
            L("Scene編集", "Scene Edit"),
            L("Sceneビューで毛を表示し、3D空間で直接編集できるようにします。",
              "Displays fur in the Scene view and enables direct editing in 3D space."));
        public static GUIContent BrushModeLabel => new GUIContent(
            L("ブラシ", "Brush"),
            L("ドラッグした方向に毛の向きを変更します。基本的な編集モードです。",
              "Changes fur direction in the drag direction. The basic editing mode."));
        public static GUIContent EraserModeLabel => new GUIContent(
            L("消しゴム", "Eraser"),
            L("毛の傾きを0にリセットします。毛を直立させるツールです。",
              "Resets fur tilt to 0. A tool to make fur stand upright."));
        public static GUIContent BlurModeLabel => new GUIContent(
            L("ぼかし", "Blur"),
            L("周囲の毛の向きを平均化してなめらかにします。",
              "Averages surrounding fur directions for a smooth result."));
        public static GUIContent PinchModeLabel => new GUIContent(
            L("つまむ", "Pinch"),
            L("毛の向きをブラシの中心に向けて集めます。",
              "Gathers fur directions toward the center of the brush."));
        public static GUIContent SpreadModeLabel => new GUIContent(
            L("拡散", "Spread"),
            L("毛の向きをブラシの中心から外側に広げます。",
              "Spreads fur directions outward from the center of the brush."));
        public static GUIContent MaskModeLabel => new GUIContent(
            L("マスク", "Mask"),
            L("編集範囲を制限するマスクを設定します。選択した領域のみ編集可能になります。",
              "Sets a mask to restrict the edit area. Only selected regions can be edited."));
    }
}
