# ソースからのビルド

Windows 11 x64、`global.json`に指定した.NET SDK、Python 3、Node.jsを使用します。
NuGetの依存関係取得にはネットワーク接続が必要です。依存バージョンは各`packages.lock.json`で固定しています。

GitHubの構成はリポジトリ直下に`src`、`tests`、`scripts`等を配置しています。
元の開発ソース・プロジェクト・試験コード・`scripts/build.py`は変更していません。
次の補助スクリプトは`Work/rebuild/<実行日時>/Source`へ必要な入力をコピーし、元の相対パス構成でビルドします。
元のファイルを上書きせず、既存の作業領域も再利用しません。

```powershell
python scripts/build_repository.py --stage --test
```

最初にlibsodiumの固定SHA-256と公式minisign署名、およびTinyMCE npmアーカイブの固定SHA-512を検証します。
検証失敗時は中止します。コピー内でTinyMCE資産を再梱包し、Viewerを先に発行してBuilderへ埋め込みます。
生成先は表示された`Work/stage`です。配布済みの正式EXEは変更しません。

再ビルドせず入力だけ確認する場合：

```powershell
python scripts/build_repository.py --check-inputs
```

既存の基本回帰グループは131項目相当です。145件というリリース時の実績は、これにレイアウト追加14項目を含む集計です。
追加レイアウト検証はビルド後、表示された実行日時フォルダー内で次のように実行できます。

```powershell
.\Work\bin\Tests\Release\net10.0-windows\win-x64\Tests.exe --layout --run-root=<その実行日時フォルダー内のWorkにある新しい試験フォルダーの絶対パス>
```

テストに記載された固定パスワード・本文・URL・添付データは公開可能なダミーです。実際の利用者のメッセージや認証情報は含めていません。
暗号化EXE、個人設定INI、ローカル検証ログ、開発用の履歴・保護スナップショットは公開対象外です。

アイコンは同梱済みです。再生成用のアイコンスクリプトを使う場合だけPillowが必要です。
TinyMCEの上流ソースは`vendor/tinymce-source-8.10.0.tar.gz`に、元のビルドスクリプトと依存管理ファイルを含めて提供しています。
