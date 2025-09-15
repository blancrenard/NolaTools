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

        // UI methods
        void OnGUI()
        {
            if (isShuttingDown) return;
            if (settings == null) return;

            // 一時メッシュ用の固定値は保存せず、使用時にのみ適用する（OnGUIでは書き込まない）

            bool scrollBegun = false;
            try
            {
                using (var cc = new EditorGUI.ChangeCheckScope())
                {
                    scr = EditorGUILayout.BeginScrollView(scr);
                    scrollBegun = true;

                    // アバター選択＆レンダラー設定UI（統合）
                    DrawAvatarAndRendererSelection();

                    // アバターが指定されていない場合は他の項目を非表示
                    if (avatarObject == null)
                    {
                        EditorGUILayout.Space(AppSettings.LARGE_SPACE);
                        EditorGUILayout.HelpBox("アバターオブジェクトを選択してください。", MessageType.Info);
                    }
                    else
                    {
                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        DrawNormalMapSettings();

                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        DrawSphereMaskSettings();

                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        // クリック島マスクの枠をマスク生成枠より上に移動
                        DrawUVIslandClickUI();

                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        DrawBoneMaskSettings();

                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        DrawMaskGenerationSettings();

                        GUILayout.Space(AppSettings.LARGE_SPACE);

                        DrawPreview();
                    }

                    if (cc.changed)
                    {
                        // 何もしていない時は保存しない方針: Dirty のみ。保存は明示操作時や確定操作時に実施
                        // Undo は各入力ハンドラ内で変更前に記録する方針とし、ここでは記録しない
                        UndoRedoUtils.SetDirtyOnly(settings);
                    }
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_GUI_DRAW, ex));
            }
            finally
            {
                if (scrollBegun)
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

    }
}
#endif
