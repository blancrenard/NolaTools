#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Data;
using Mask.Generator.Utils;
using Mask.Generator.Constants;

namespace Mask.Generator
{
    public partial class FurMaskGenerator
    {
        // アバター選択＆レンダラー設定UI（統合）
        void DrawAvatarAndRendererSelection()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                // アバター選択フィールド
                EditorGUI.BeginChangeCheck();
                avatarObject = (GameObject)EditorGUILayout.ObjectField(
                    UILabels.AVATAR_LABEL,
                    avatarObject,
                    typeof(GameObject),
                    true);
                if (EditorGUI.EndChangeCheck())
                {
                    SwitchAvatar();
                }

                // アバターが選択されている場合のみレンダラー関連UIを表示
                if (avatarObject != null)
                {
                    EditorGUILayout.Space();

                    // アバター検出ボタン
                    if (GUILayout.Button(UILabels.DETECT_AVATAR_RENDERERS_BUTTON))
                    {
                        AutoDetectRenderers();
                        StoreAvatarAndRendererReferences();
                        UIDrawingUtils.RefreshUI();
                    }

                    EditorGUILayout.Space();

                    // 表示中のレンダラーのみ追加ボタン（各リストを一度クリアしてから再分類）
                    if (GUILayout.Button(UILabels.ADD_VISIBLE_RENDERERS_BUTTON))
                    {
                        AddVisibleRenderers();
                        StoreAvatarAndRendererReferences();
                        UIDrawingUtils.RefreshUI();
                    }

                    EditorGUILayout.Space();

                    // Renderer lists（独自IMGUI版）
                    UIDrawingUtils.DrawRendererList(avatarRenderers, UILabels.AVATAR_RENDERERS_LABEL);

                    EditorGUILayout.Space();

                    UIDrawingUtils.DrawRendererList(clothRenderers, UILabels.CLOTH_RENDERERS_LABEL);
                }
            });
        }

        // アバター切り替え処理
        void SwitchAvatar()
        {
            if (avatarObject == null)
            {
                // アバターがクリアされた場合
                ClearSettingsForNewAvatar();
                ClearUVIslandCache(); // キャッシュをクリア
                UIDrawingUtils.RefreshUI();
                return;
            }

            // 前アバターのプレビューなどをクリア
            CleanupTextures();
            ClearUVIslandCache(); // アバター切り替え時にキャッシュをクリア

            // 新しいアバターが設定された場合
            if (TryLoadSettingsForAvatar(avatarObject, out FurMaskSettings loadedSettings))
            {
                settings = loadedSettings;
                // 設定を読み込んだ場合、レンダラーリストを復元
                RestoreAvatarAndRendererReferences();
            }
            else
            {
                // 新しいアバターの場合、レンダラーを自動検出
                // アバター固有の設定アセットを作成してから反映
                settings = CreateSettingsForAvatar(avatarObject);
                AutoDetectRenderers();
                StoreAvatarAndRendererReferences();
                
                // レンダラー検出後、テクスチャサイズも自動設定
                try
                {
                    AutoDetectTextureSize();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FurMaskGenerator] テクスチャサイズの自動検出に失敗しました: {ex.Message}");
                }
            }
            UIDrawingUtils.RefreshUI();
        }
    }
}

#endif


