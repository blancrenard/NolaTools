# FurMaskGenerator VPM Package

Unity Editor拡張ツール - VRChatアバター用のファーマスク生成ツール

## VCCでのインストール方法

1. VRChat Creator Companionを開く
2. 「Settings」→「Packages」→「Add Repository」
3. 以下のURLを入力: `https://nolatools.github.io/FurMaskGenerator/index.json`
4. プロジェクトでFurMaskGeneratorパッケージを追加

## 主な機能

- **スフィアマスク**: 3D空間上でスフィアを配置して毛の長さを制御
- **UVマスク**: UV座標をクリックして毛の長さを制御  
- **ボーンマスク**: ボーン単位で毛の長さを制御
- **ノーマルマップ**: 毛の向きを制御
- **自動検出機能**: アバターと服のレンダラー自動検出
- **プレビュー機能**: テクスチャプレビューウィンドウ

## 使用方法

1. `Tools/NolaTools/FurMaskGenerator` からウィンドウを開く
2. アバターオブジェクトを選択
3. 自動設定ボタンでレンダラーを設定
4. マスクを配置・設定
5. 「マスクを生成」ボタンでテクスチャを生成

## システム要件

- Unity 2022.3 LTS 以降
- VRChat SDK 2022.1.1 以降

## ライセンス

MIT License

## 開発者向け情報

### リリース方法

1. `Packages/com.nolatools.furmaskgenerator/package.json`のバージョンを更新
2. GitHubでActionsタブから「Build Release」を実行
3. 自動的にリリースが作成され、VPMリストが更新される

### パッケージ構造

```
Packages/
└── com.nolatools.furmaskgenerator/
    ├── package.json
    └── Editor/
        ├── FurMaskGenerator.cs
        ├── Core/
        ├── Data/
        ├── Processing/
        ├── UI/
        └── Utils/
```