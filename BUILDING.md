# CipherCapsule v1.1.3をソースからビルド

Windows 11 x64、`global.json`に指定した.NET SDK、Python 3、Node.jsを使用します。
NuGetの依存関係取得にはネットワーク接続が必要です。依存バージョンは各`packages.lock.json`で固定しています。

GitHubの構成はリポジトリ直下に`src`、`tests`、`scripts`等を配置しています。
アプリケーションソースとプロジェクト設定はv1.1.3の正式成果物に対応します。公開用`scripts/build.py`は正式開発版と同じ試験グループを実行し、配布していない旧版試験データを要求しない引数を使用します。内部の発行・クリーンアップ用スクリプトや個人用検証記録は含めません。
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

`--test`はCore 90件、Editor 18件、HTML 7件、追加4件、終了処理10件、レイアウト14件、v1.1.2追加32件、v1.1.3基本改修33件、Builderフォルダ操作24件、Viewerフォルダ操作33件、200%拡大相当2件の計267件を実行します。
結果は作業フォルダーの`Work/test-results/all-results.json`に集計します。WebView2 Runtimeと対話可能なWindowsデスクトップが必要です。ネイティブGUI試験は一時的にウィンドウを表示し、ダミー文字列をクリップボードへコピーします。
正式発行時の最終記録は267件成功／0件失敗です。合格記録を引き継ぎ、追加変更を限定検証した集計であり、公開工程で全件を再実行していません。公開用の依存入力・試験グループ呼出し・件数集計だけを限定検証しました。

Coreには`--skip-verified-large --skip-verified-legacy`を渡します。256MiBストリーミング試験と、同梱していないv1.0.0生成EXEを使う互換性試験は今回の267件に含めません。前版の合格記録は新規実行として計上していません。
256MiB試験を追加実行する場合は、表示された作業フォルダー内の`Work/bin/Tests/Release/net10.0-windows/win-x64/Tests.exe`を`--skip-verified-legacy`だけで実行してください。

`--stage`（`--test`なし）はViewerとBuilderだけをRelease / win-x64、SelfContained / PublishSingleFile / PublishTrimmed=falseで発行します。
Builderは`CipherCapsule_v1.1.3.exe`、Viewerは圧縮済み`CipherCapsule_Viewer.exe`です。再ビルド結果は配布済みの正式EXEを置き換えません。
バイナリの一致を保証する再現可能ビルドの認証は行っていません。

内部ダミーを使う`--verify-gui113`、`--inspect-gui113`、`--prepare-viewer-folder-gui113`は通常試験に含めず、過去の個別手順用です。リポジトリだけではその検証データは揃いません。任意の診断用分岐`--exit-trace`はローカルのダミー生成EXEを必要とし、通常の試験には含めません。`--zoom-before`は倍率診断用ハーネスで、旧版の測定結果そのものは含みません。

テストに記載された固定パスワード・本文・URL・添付データは公開可能なダミーです。実際の利用者のメッセージや認証情報は含めていません。
暗号化EXE、個人設定INI、ローカル検証ログ、開発用の履歴・保護スナップショットは公開対象外です。

アイコンは同梱済みです。再生成用のアイコンスクリプトを使う場合だけPillowが必要です。
TinyMCEの上流ソースは`vendor/tinymce-source-8.10.0.tar.gz`に、元のビルドスクリプトと依存管理ファイルを含めて提供しています。
