# FurMaskGenerator

Unity Editor拡張ツール - VRChatアバター用の毛マスク生成ツール

## 概要

FurMaskGeneratorは、VRChatアバター用の毛（ファー）マスクテクスチャを自動生成するUnity Editor拡張ツールです。スフィアマスク、UVマスク、ボーンマスク、ノーマルマップを組み合わせて、リアルな毛の長さマスクを生成できます。

## 主な機能

### 1. マスク生成機能
- **スフィアマスク**: 3D空間上で球体を配置して毛の長さを制御
- **UVマスク**: UV座標をクリックして毛の長さを制御
- **ボーンマスク**: ボーン単位で毛の長さを制御
- **ノーマルマップ**: 毛の向きを制御

### 2. 自動検出機能
- アバターと服のレンダラー自動検出
- テクスチャサイズ自動検出
- ファーノーマルマップ自動設定

### 3. プレビュー機能
- リアルタイムプレビュー
- テクスチャプレビューウィンドウ
- シーンビューでの視覚的確認

## 使用方法

### 基本的な使用手順

1. **アバター設定**
   - `Tools/NolaTools/FurMaskGenerator` からウィンドウを開く
   - アバターオブジェクトを選択
   - 自動設定ボタンでレンダラーを設定

2. **マスク設定**
   - スフィアマスク: シーン上でクリックしてスフィアを配置
   - UVマスク: シーン上でクリックしてUV座標を指定
   - ボーンマスク: ボーンを選択してマスク値を設定

3. **マスク生成**
   - テクスチャサイズと毛の長さを設定
   - 「マスクを生成」ボタンでテクスチャを生成
   - プレビューで確認後、保存

### 高度な設定

#### スフィアマスク
- **位置**: 3D空間での位置
- **半径**: マスクの影響範囲
- **グラデーション**: エッジのぼかし具合
- **濃さ**: マスクの強度（0.1-1.0）
- **ミラー**: X軸反転での対称配置

#### UVマスク
- **UV範囲**: 隣接するUVの検出範囲
- **閾値**: UV距離の判定閾値
- **表示**: シーンビューでの可視化

#### ボーンマスク
- **ボーン選択**: ヒエラルキーからボーンを選択
- **マスク値**: 0（無効）〜1（最大効果）

#### ノーマルマップ
- **マテリアル選択**: 対象マテリアルを指定
- **ノーマルマップ**: 毛の向きを制御するテクスチャ
- **強度**: ノーマルマップの影響度
- **自動検出**: ファーノーマルマップの自動設定

## ファイル構造

```
FurMaskGenerator/
├── Scripts/
│   ├── Core/                    # コア機能
│   │   ├── FurMaskGenerator.Core.cs
│   │   ├── FurMaskGenerator.Features.cs
│   │   └── UI/                  # UI関連
│   │       ├── FurMaskGenerator.UI.cs
│   │       ├── FurMaskGenerator.UI.Avatar.cs
│   │       ├── FurMaskGenerator.UI.BoneMask.cs
│   │       ├── FurMaskGenerator.UI.MaskGeneration.cs
│   │       ├── FurMaskGenerator.UI.NormalMap.cs
│   │       ├── FurMaskGenerator.UI.Preview.cs
│   │       ├── FurMaskGenerator.UI.Scene.cs
│   │       ├── FurMaskGenerator.UI.Sphere.cs
│   │       └── FurMaskGenerator.UI.UV.cs
│   ├── Data/                    # データクラス
│   │   └── FurMaskData.cs
│   ├── Constants/               # 定数
│   │   └── UIConstants.cs
│   ├── Processing/              # 処理ロジック
│   │   ├── DistanceMaskBaker/
│   │   └── TextureProcessor/
│   ├── Utils/                   # ユーティリティ
│   │   ├── EditorAssetUtils.cs
│   │   ├── EditorCoreUtils.cs
│   │   ├── EditorGizmoUtils.cs
│   │   ├── EditorMeshUtils.cs
│   │   ├── EditorNameFilters.cs
│   │   ├── EditorObjectUtils.cs
│   │   ├── EditorPathUtils.cs
│   │   ├── EditorTextureUtils.cs
│   │   ├── EditorUIUtils.cs
│   │   ├── EditorUvUtils.cs
│   │   ├── FurNormalMapUtils.cs
│   │   └── RendererDetectionUtils.cs
│   └── UI/                      # UI関連
│       └── TexturePreviewWindow/
└── AvatarSettings/              # アバター別設定
```

## 技術仕様

### 対応Unityバージョン
- Unity 2022.3 LTS 以降

### 依存関係
- UnityEngine
- UnityEditor
- UnityEditorInternal（一部機能）

### アーキテクチャ
- **MVC パターン**: UI、データ、処理ロジックの分離
- **Partial クラス**: 機能別にファイル分割
- **ユーティリティクラス**: 共通処理の集約
- **イベント駆動**: シーンビューとの連携

## トラブルシューティング

### よくある問題

1. **レンダラーが検出されない**
   - アバターにAnimator（Humanoid）が設定されているか確認
   - オブジェクトがアクティブになっているか確認

2. **テクスチャが生成されない**
   - レンダラーが正しく設定されているか確認
   - マスクが適切に配置されているか確認

3. **プレビューが表示されない**
   - テクスチャの読み取り権限を確認
   - Texture Import Settingsで「Read/Write Enabled」を有効化

### ログの確認
- Unity Consoleでエラーメッセージを確認
- デバッグログは `[FurMaskGenerator]` プレフィックスで出力

## ライセンス

このツールはVRChatアバター制作用に開発されました。商用利用については個別にお問い合わせください。

## 更新履歴

### v1.0.0
- 初回リリース
- 基本的なマスク生成機能
- スフィアマスク、UVマスク、ボーンマスク対応
- ノーマルマップ対応
- 自動検出機能

## サポート

問題が発生した場合は、Unity Consoleのエラーメッセージと併せてお問い合わせください。
