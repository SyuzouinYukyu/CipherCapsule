# CipherCapsule v1.1.9をソースからビルド

Windows 11 x64、.NET SDK 10.0.402、Python 3、Node.jsを使用します。固定依存アーカイブ・対応ソース・ライセンスとNuGetロックファイルはリポジトリに同梱しています。

リポジトリ直下で実行します。

```text
python scripts/build_repository.py --stage
```

固定libsodiumハッシュ・公式署名とTinyMCE配布物の完全性を検証し、`Work/rebuild/<日時>/Source`へ隔離コピーします。`build119.py`はViewer、Builderの順でRelease / win-x64 / SelfContained / PublishSingleFile / 圧縮有効 / PublishTrimmed=falseとして発行します。出力は同じ隔離領域の`Work/stage`です。配布済み正式EXEは置き換えません。.NET Runtimeの別途インストールは不要ですが、HTML編集・表示にはMicrosoft Edge WebView2 Runtimeが必要です。

入力だけを検証する場合：`python scripts/build_repository.py --check-inputs`

TinyMCE資産を再構成する場合は、隔離コピーのSource内から`python scripts/editor_assets.py`を実行します。`editor-ui-scale.js`と`editor-bridge.js`もeditor.zipへ収録し、ベンダー資産を直接変更しません。

## v1.1.8までの既存検証結果

自動検証はUI寸法23件、JS橋渡し統合10件、切替状態・INI22件、チェックサム22件、旧新互換4件、合計81件成功／0件失敗。2026年10月24日に利用者からWindows実機GUI手動確認の全項目合格を受領しています。実デスクトップの自動操作は行っていません。物理モニターのDPI切替は未検証です。

利用者の間欠症状そのものは再現しておらず、非対話検証で確認した待機・状態遷移・失敗応答の問題への修正です。公開のための再ビルド・重複試験は行っていません。バイナリ完全一致の再現可能ビルド認証は行っていません。

## 必要時のみ行う限定検証

以下は上記隔離コピーのSourceを作業ディレクトリとし、結果先には未使用のディレクトリを指定します。既存の記録がある場合は理由なく再実行しません。

- `dotnet run --project tests/Editor118/Editor118.Tests.csproj -c Release -- <結果先>`：切替状態・非同期失敗・復旧・INI、22件。
- `dotnet run --project tests/Checksum116/Checksum116.Tests.csproj -c Release -- <Viewerテンプレート> <結果先>`：既存チェックサム回帰、22件。
- `python scripts/prepare-compat118.py`で同梱の`tests/fixtures/legacy117-crypto.zip`を展開した後、`dotnet run --project tests/Compat118/Compat118.Tests.csproj -c Release -- <旧Viewerテンプレート> <新Viewerテンプレート> <結果先>`：互換性4件。旧Viewerは旧Releaseに対応する資料から別途用意します。旧版ディレクトリは変更しません。
- `python scripts/prepare-dom118.py`と`node tests/ui-scale118.cjs "Chromeの実行ファイルの絶対パス"`：専用の非表示headlessプロファイルとローカルHTTPによるUI寸法・本文不変性・橋渡し処理。既存のWork/dom-assetsがある場合は停止します。

従来の試験・補助スクリプトは参考資料です。古いバージョンの発行処理を現行版へ実行しないでください。固定パスワード・本文は公開用ダミーで、個人INI・内部記録・生成試験EXEは含みません。

LICENSE、THIRD_PARTY_NOTICES.md、licenses、vendorの著作権表示・対応ソースを保持してください。暗号Core・コンテナー形式・依存版はv1.1.7から変更していません。

## v1.1.9の限定検証

新機能のホスト試験は `python scripts/test119.py` で97件と代表的回帰6件です。この試験の一部は同じ共通形式を実装したAndroid v1.0.3のソースを、開発時と同じ兄弟ディレクトリに要求します。公開のために試験や正式EXEの再ビルドは行っていません。
