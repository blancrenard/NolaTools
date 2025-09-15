#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    /// <summary>
    /// FurMaskGenerator - Unity Editor拡張ウィンドウ
    /// メインエントリポイントファイル
    /// </summary>
    public partial class FurMaskGenerator : EditorWindow
    {
        // このファイルはUnityのレイアウトファイルが参照するためのエントリポイントです
        // 実際の処理はpartialクラスの他のファイルに実装されています:
        // - FurMaskGenerator.Core.cs: コア機能とウィンドウ管理
        // - FurMaskGenerator.UI.cs: UI描画とユーザーインターフェース
        // - FurMaskGenerator.Features.cs: 機能実装
        // - FurMaskGenerator.BoneMaskPresets.cs: ボーンマスクプリセット

        public FurMaskGenerator()
        {
        }
    }
}
#endif
