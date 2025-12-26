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

        // ツールチップ付きラベル
        public static readonly GUIContent BrushSizeLabel = new GUIContent(
            "ブラシのサイズ",
            "ブラシの直径を設定します。Alt+マウスホイールでも調整可能です。");
        public static readonly GUIContent BrushPowerLabel = new GUIContent(
            "ブラシの強さ",
            "ブラシの適用強度を設定します。値が大きいほど効果が強くなります。");
        public static readonly GUIContent InclinedLabel = new GUIContent(
            "毛の傾き",
            "毛の傾斜角度を設定します。0で直立、1に近いほど寝かせます。");
        public static readonly GUIContent DirectionOnlyLabel = new GUIContent(
            "向きのみ変更",
            "有効にすると、毛の向きのみを変更し傾きは維持します。");
        public static readonly GUIContent InclinedOnlyLabel = new GUIContent(
            "傾きのみ変更",
            "有効にすると、毛の傾きのみを変更し向きは維持します。");
        public static readonly GUIContent MirrorLabel = new GUIContent(
            " ミラー",
            "左右対称に編集します。アバターの対称部位に同時に適用されます。");
        public static readonly GUIContent InvertLabel = new GUIContent(
            "効果反転(拡散)",
            "つまむモードの効果を反転させます。");
        public static readonly GUIContent DotIntervalFormat = new GUIContent("ドット間隔: {0}");

        // 拡大縮小のラベル
        public static readonly GUIContent ScaleLabel = new GUIContent(
            "拡大縮小",
            "キャンバスの表示倍率を設定します。Ctrl+マウスホイールでも調整可能です。");

        // 自動設定のツールチップ
        public static readonly GUIContent AutoSetupSurfaceLiftLabel = new GUIContent(
            "毛の傾き",
            "自動生成される毛の傾き具合を設定します。0で直立、0.95で面に沿った向きになります。");
        public static readonly GUIContent AutoSetupRandomnessLabel = new GUIContent(
            "ランダム性",
            "毛の向きにランダムなばらつきを加えます。自然な見た目になります。");

        // モード選択ボタン（左メニュー）
        public static readonly GUIContent SceneEditingLabel = new GUIContent(
            "Scene編集",
            "Sceneビューで毛を表示し、3D空間で直接編集できるようにします。");
        public static readonly GUIContent BrushModeLabel = new GUIContent(
            "ブラシ",
            "ドラッグした方向に毛の向きを変更します。基本的な編集モードです。");
        public static readonly GUIContent EraserModeLabel = new GUIContent(
            "消しゴム",
            "毛の傾きを0にリセットします。毛を直立させるツールです。");
        public static readonly GUIContent BlurModeLabel = new GUIContent(
            "ぼかし",
            "周囲の毛の向きを平均化してなめらかにします。");
        public static readonly GUIContent PinchModeLabel = new GUIContent(
            "つまむ",
            "毛の向きをブラシの中心に向けて集めます。");
        public static readonly GUIContent SpreadModeLabel = new GUIContent(
            "拡散",
            "毛の向きをブラシの中心から外側に広げます。");
        public static readonly GUIContent MaskModeLabel = new GUIContent(
            "マスク",
            "編集範囲を制限するマスクを設定します。選択した領域のみ編集可能になります。");
    }
}
