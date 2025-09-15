#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Data;
using Mask.Generator.Constants;
using Mask.Generator.Utils;

namespace Mask.Generator
{
	public partial class FurMaskGenerator
	{
		// Bone mask settings UI
		void DrawBoneMaskSettings()
		{
			UIDrawingUtils.DrawInUIBox(() =>
			{
				// タイトル折りたたみ（共通ヘルパ使用）
				foldoutBoneSection = UIDrawingUtils.DrawSectionFoldout(foldoutBoneSection, UILabels.BONE_SECTION_TITLE);

				// データ初期化とプリセット準備は折りたたみ状態に関わらず維持
				if (settings.boneMasks == null)
				{
					settings.boneMasks = new List<BoneMaskData>();
					UndoRedoUtils.RecordUndoAndSetDirty(settings, "Initialize Bone Masks");
				}
				EnsureHumanoidPreset();
				EnsureNonHumanoidPreset();

				if (!foldoutBoneSection) return;

				// グループ化表示（Body / Head / Arm / Hand / Leg / Foot / Ear / Tail）
				var groupOrder = new List<string>(GameObjectConstants.GROUP_ORDER);
				var groups = new Dictionary<string, List<int>>(); // group -> indices
				for (int i = 0; i < settings.boneMasks.Count; i++)
				{
					var bm = settings.boneMasks[i];
					string tail = GetTailName(bm.bonePath);
					string grp = GetGroupLabel(tail);
					if (!groups.TryGetValue(grp, out var list)) { list = new List<int>(); groups[grp] = list; }
					list.Add(i);
				}

				foreach (var grp in groupOrder)
				{
					if (!groups.TryGetValue(grp, out var idxs) || idxs.Count == 0) continue;
					// 代表値（最初の要素）
					float cur = settings.boneMasks[idxs[0]].value;
					float nv = EditorGUILayout.Slider(grp, cur, 0f, 1f);
					if (Mathf.Abs(nv - cur) > 1e-6f)
					{
						foreach (int k in idxs) settings.boneMasks[k].value = nv;
						UndoRedoUtils.SetDirtyOnly(settings);
					}
				}
			});
		}

		// プリセット補助は他partial (FurMaskGenerator.BoneMaskPresets.cs) の実装を使用します。
	}
}

#endif


