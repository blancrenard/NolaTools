using System;
using System.Collections.Generic;
using GroomingTool2.Core;
using GroomingTool2.Services;
using GroomingTool2.State;
using Unity.Collections;
using UnityEngine;
using Managers = GroomingTool2.Managers;

namespace GroomingTool2.Managers
{
    internal sealed class FurDataManager
    {
        private readonly BrushManager brushManager;
        private readonly IStrokeService strokeService;
        private readonly BrushProcessor brushProcessor;
        private ISymmetryService symmetryService;
        private VertexSymmetryMapper vertexSymmetryMapper;
        private GroomingTool2State state;

        private NativeArray<FurData> furData;
        private readonly List<Vector2Int> debugMirrorPoints = new List<Vector2Int>(4096);

        public NativeArray<FurData> Data => furData;
        // マスク（プールされたNativeArray、ストローク開始時にセット/クリア）
        private NativeArray<byte> pooledMaskBuffer;
        private bool maskActive;
        public bool HasMask => maskActive && pooledMaskBuffer.IsCreated && pooledMaskBuffer.Length > 0;

        public FurDataManager(BrushManager brushManager, IStrokeService strokeService, GroomingTool2State state)
        {
            this.brushManager = brushManager;
            this.strokeService = strokeService;
            this.state = state;

            furData = new NativeArray<FurData>(Common.TexSizeSquared, Allocator.Persistent);
            brushProcessor = new BrushProcessor(brushManager, state);
        }

        /// <summary>
        /// 毛データを初期状態（ゼロクリア）にリセットする
        /// </summary>
        public void ClearAllData()
        {
            for (int i = 0; i < furData.Length; i++)
            {
                furData[i] = new FurData { Dir = 0, Inclined = 0f };
            }
            // マスクも無効化しておく
            ClearMask();
        }

        /// <summary>
        /// VertexSymmetryMapperを設定
        /// </summary>
        public void SetVertexSymmetryMapper(VertexSymmetryMapper mapper)
        {
            vertexSymmetryMapper = mapper;
            symmetryService = mapper != null ? new SymmetryService(mapper) : null;
        }
        
        /// <summary>
        /// VertexSymmetryMapperを取得
        /// </summary>
        public VertexSymmetryMapper GetVertexSymmetryMapper()
        {
            return vertexSymmetryMapper;
        }

        /// <summary>
        /// マスクをセット（ストローク開始時）
        /// プールされたバッファを再利用してアロケーションを削減
        /// </summary>
        public void SetMask(byte[] maskBytes)
        {
            if (maskBytes == null || maskBytes.Length != Common.TexSizeSquared)
            {
                maskActive = false;
                return;
            }

            // バッファが未作成または サイズが異なる場合のみ再作成
            if (!pooledMaskBuffer.IsCreated || pooledMaskBuffer.Length != Common.TexSizeSquared)
            {
                if (pooledMaskBuffer.IsCreated)
                    pooledMaskBuffer.Dispose();
                pooledMaskBuffer = new NativeArray<byte>(Common.TexSizeSquared, Allocator.Persistent);
            }

            // データをコピー
            NativeArray<byte>.Copy(maskBytes, pooledMaskBuffer);
            maskActive = true;
        }

        /// <summary>
        /// マスクをクリア（ストローク終了時）
        /// バッファは保持したまま、アクティブフラグのみクリア
        /// </summary>
        public void ClearMask()
        {
            maskActive = false;
        }

        /// <summary>
        /// マスクバッファが作成されていることを保証する（Jobに渡すため）
        /// </summary>
        private void EnsureMaskBufferCreated()
        {
            if (!pooledMaskBuffer.IsCreated || pooledMaskBuffer.Length != Common.TexSizeSquared)
            {
                if (pooledMaskBuffer.IsCreated)
                    pooledMaskBuffer.Dispose();
                pooledMaskBuffer = new NativeArray<byte>(Common.TexSizeSquared, Allocator.Persistent);
            }
        }

        public void Dispose()
        {
            maskActive = false;
            if (pooledMaskBuffer.IsCreated)
            {
                pooledMaskBuffer.Dispose();
            }
            brushProcessor?.Dispose();
            if (furData.IsCreated)
            {
                furData.Dispose();
            }
        }




        private void UpdateFurDataJobs(List<Vector2Int> points, float radian, bool eraserMode, bool blurMode, bool pinchMode, bool inclinedOnly, bool dirOnly, bool pinchInverted)
        {
            // マスクがない場合でも、空のバッファを確保しておく（Jobに渡すため）
            EnsureMaskBufferCreated();
            
            // プールされたマスクバッファを使用（アロケーションなし）
            // HasMaskフラグでマスクの有効性を判断するため、常にバッファを渡す
            brushProcessor.ProcessBrushJobs(furData, points, radian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted, pooledMaskBuffer, HasMask);
        }

        public void UpdateWithMirror(List<Vector2Int> points, float radian, bool enableMirror, bool eraserMode, bool blurMode, bool pinchMode, bool inclinedOnly, bool dirOnly, bool pinchInverted)
        {
            // ミラーが無効、または初期化されていない場合は通常処理のみ
            if (!enableMirror)
            {
                debugMirrorPoints.Clear();
                UpdateFurDataJobs(points, radian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
                return;
            }
            
            if (symmetryService == null || vertexSymmetryMapper == null || !vertexSymmetryMapper.IsInitialized)
            {
                Debug.LogWarning($"[FurDataManager] ミラーが有効ですが、VertexSymmetryMapperが初期化されていません: mapper={vertexSymmetryMapper != null}, initialized={vertexSymmetryMapper?.IsInitialized ?? false}");
                debugMirrorPoints.Clear();
                UpdateFurDataJobs(points, radian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
                return;
            }
            
            // サービス層を使用して対称点を取得
            debugMirrorPoints.Clear();
            var mirrorBuffer = symmetryService.GetMirrorPoints(points, out bool allMirrored);
            debugMirrorPoints.AddRange(mirrorBuffer);

            // ミラー側が塗れない場合の処理
            if (!allMirrored)
            {
                // UV内のみ編集する設定がオフの場合は、ブラシ側だけ処理してミラーはスキップ
                if (!state.RestrictEditToUvRegion)
                {
                    UpdateFurDataJobs(points, radian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
                }
                // UV内のみ編集する設定がオンの場合は、両方スキップ（元の動作）
                return;
            }

            // 元の点を塗る
            UpdateFurDataJobs(points, radian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
            
            // 対称点を塗る（ミラー側のストローク方向は3D空間経由で計算）
            if (mirrorBuffer.Count > 0)
            {
                // 代表点のUV座標を取得（最初のポイント）
                var representativePoint = mirrorBuffer[0];
                Vector2 srcUV = new Vector2(
                    representativePoint.x / (float)Core.Common.TexSize,
                    1.0f - (representativePoint.y / (float)Core.Common.TexSize)
                );
                
                // 3D空間経由でミラー方向を計算（失敗時はスキップ）
                if (!vertexSymmetryMapper.TryCalculateMirrorDirectionVia3D(srcUV, radian, out float mirrorRadian))
                {
                    return;
                }
                
                UpdateFurDataJobs(mirrorBuffer, mirrorRadian, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
            }
        }

        public void FlipYAxis()
        {
            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    int index = Common.GetIndex(x, y);
                    var data = furData[index];
                    var rad = data.Dir * 0.1f * Mathf.Deg2Rad;
                    var cos = Mathf.Cos(rad);
                    var sin = Mathf.Sin(rad);
                    var newRad = Mathf.Atan2(-sin, cos);
                    var dir = Mathf.RoundToInt(newRad * Mathf.Rad2Deg * 10f);
                    var wrappedDir = AngleLut.WrapDir(dir);

                    // 念のため境界チェック
                    if (wrappedDir >= AngleLut.MinDir && wrappedDir <= AngleLut.MaxDir)
                    {
                        data.Dir = wrappedDir;
                    }
                    else
                    {
                        data.Dir = 0; // デフォルト値
                    }
                    furData[index] = data;
                }
            }
        }

        public void SaveToSerialized(FurDataSerialized serialized)
        {
            var total = Common.TexSizeSquared;
            serialized.Dir = new int[total];
            serialized.Inclined = new float[total];

            for (var i = 0; i < total; i++)
            {
                serialized.Dir[i] = furData[i].Dir;
                serialized.Inclined[i] = furData[i].Inclined;
            }
        }

        public void RestoreFromSerialized(FurDataSerialized serialized)
        {
            if (serialized.Dir == null || serialized.Inclined == null)
                return;

            var total = Mathf.Min(serialized.Dir.Length, serialized.Inclined.Length);
            total = Mathf.Min(total, Common.TexSizeSquared);

            for (var i = 0; i < total; i++)
            {
                var dirValue = serialized.Dir[i];
                var wrappedDir = AngleLut.WrapDir(dirValue);

                // 念のため境界チェック
                if (wrappedDir >= AngleLut.MinDir && wrappedDir <= AngleLut.MaxDir)
                {
                    furData[i] = new FurData
                    {
                        Dir = wrappedDir,
                        Inclined = Mathf.Min(serialized.Inclined[i], 0.95f)
                    };
                }
                else
                {
                    furData[i] = new FurData
                    {
                        Dir = 0, // デフォルト値
                        Inclined = Mathf.Min(serialized.Inclined[i], 0.95f)
                    };
                }
            }
        }

        public void LoadNormalMap(Texture2D texture)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));

			Color32[] colors;
			if (texture.width == Common.TexSize && texture.height == Common.TexSize)
			{
				colors = texture.GetPixels32();
			}
			else
			{
				// 最近傍リサンプリング（ブレ/平滑化を避ける）
				var src = texture.GetPixels32();
				colors = new Color32[Common.TexSizeSquared];
				for (var y = 0; y < Common.TexSize; y++)
				{
					int sy = Mathf.Clamp(Mathf.RoundToInt(((y + 0.5f) * texture.height / Common.TexSize) - 0.5f), 0, texture.height - 1);
					for (var x = 0; x < Common.TexSize; x++)
					{
						int sx = Mathf.Clamp(Mathf.RoundToInt(((x + 0.5f) * texture.width / Common.TexSize) - 0.5f), 0, texture.width - 1);
						colors[y * Common.TexSize + x] = src[sy * texture.width + sx];
					}
				}
			}
			for (var y = 0; y < Common.TexSize; y++)
            {
				for (var x = 0; x < Common.TexSize; x++)
                {
					// 出力時にYを反転しているため、読み込み時もYを反転して取り出す
					int iy = Common.TexSize - 1 - y;
					var color = colors[iy * Common.TexSize + x];
                    var normal = ColorToNormal(color);
					// 出力時にY成分を反転しているため、読込時に元へ戻す
					normal.y = -normal.y;
                    var angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
                    var dir = Mathf.RoundToInt(angle * 10f);
                    var wrappedDir = AngleLut.WrapDir(dir);
                    var length = Mathf.Sqrt(normal.x * normal.x + normal.y * normal.y);
                    length = Mathf.Clamp(length, 0f, 0.95f);

                    int index = Common.GetIndex(x, y);
                    // 念のため境界チェック
                    if (wrappedDir >= AngleLut.MinDir && wrappedDir <= AngleLut.MaxDir)
                    {
                        furData[index] = new FurData
                        {
                            Dir = wrappedDir,
                            Inclined = length
                        };
                    }
                    else
                    {
                        furData[index] = new FurData
                        {
                            Dir = 0, // デフォルト値
                            Inclined = length
                        };
                    }
                }
            }
        }

		private static Vector3 ColorToNormal(Color32 color)
        {
            var x = color.r / 255f * 2f - 1f;
            var y = color.g / 255f * 2f - 1f;
            var z = color.b / 255f * 2f - 1f;
			return new Vector3(x, y, z);
        }



        /// <summary>
        /// UndoStateからデータを復元する
        /// </summary>
        public void RestoreFromUndoState(UndoState undoState)
        {
            if (undoState == null)
                return;

            int i = 0;
            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    furData[i++] = undoState.FurData[x, y];
                }
            }
        }

        /// <summary>
        /// ミラー処理が有効で初期化済みかどうか
        /// </summary>
        public bool IsMirrorInitialized => symmetryService != null && vertexSymmetryMapper != null && vertexSymmetryMapper.IsInitialized;

        /// <summary>
        /// データ座標のリストからミラー位置のデータ座標を取得
        /// </summary>
        /// <param name="dataCoords">データ座標のリスト</param>
        /// <returns>ミラー位置のデータ座標セット</returns>
        public HashSet<Vector2Int> GetMirrorDataCoords(IEnumerable<Vector2Int> dataCoords)
        {
            var result = new HashSet<Vector2Int>();
            
            if (symmetryService == null || vertexSymmetryMapper == null || !vertexSymmetryMapper.IsInitialized)
                return result;

            var inputList = new List<Vector2Int>(dataCoords);
            var mirrorPoints = symmetryService.GetMirrorPoints(inputList, out _);
            
            foreach (var point in mirrorPoints)
            {
                result.Add(point);
            }
            
            return result;
        }

        /// <summary>
        /// データ座標のリストからミラー位置のデータ座標を取得
        /// </summary>
        /// <param name="dataCoords">データ座標のリスト</param>
        /// <returns>ミラー位置のデータ座標リスト</returns>
        public List<Vector2Int> GetMirrorDataCoords(List<Vector2Int> dataCoords)
        {
            if (symmetryService == null || vertexSymmetryMapper == null || !vertexSymmetryMapper.IsInitialized)
                return new List<Vector2Int>();

            return symmetryService.GetMirrorPoints(dataCoords, out _);
        }

    }
}
