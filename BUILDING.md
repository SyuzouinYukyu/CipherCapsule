# Windows v1.1.12 ビルド

Windows 11 x64、Python、Node.js、.NET SDK 10.0.404を使用します。Runtime/ILLink 10.0.13、NuGet依存関係はプロジェクトとロックファイルに固定しています。

公開リポジトリ直下で `python scripts/build_repository.py --check-inputs` により固定依存入力とlibsodium公式署名を検証できます。ビルドは `python scripts/build_repository.py --stage` です。

補助スクリプトは未使用の `Work/rebuild/<時刻>/Source` に公開入力を複製し、`scripts/build.py` → `scripts/build1112.py` を実行します。Viewer → テンプレート → Builderの順で、Release / win-x64 / SelfContained / PublishSingleFile / PublishTrimmed=false、SingleFile圧縮有効で発行します。出力は表示される `Work/stage1112/Builder/CipherCapsule_v1.1.12.exe` です。既存ステージ・ログを上書きせず、配布済みEXEは置き換えません。

libsodium DLLおよびTinyMCE資産は固定版を再利用します。SDKの自動更新やAndroidビルドは行いません。依存・ライセンス資料は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) と [過去の詳細資料](BUILDING_PREVIOUS_REFERENCE.md) を参照してください。

関連ロジック試験の再現: 分離された新しいコピーのSourceで `dotnet run --project tests/Secure1112/Secure1112.csproj -c Release -p:NuGetAudit=false -- <未使用の絶対パス>`。この試験は指定ダミー領域にACL・ジャンクションを作成し、専用子プロセスの異常終了を検証します。実データを指定しないでください。リンク作成が制限される環境では該当試験が失敗する場合があります。

開発時の34件成功／失敗0件と利用者GUI5件合格は既存の検証実績です。今回の公開工程では再ビルド・再試験していません。物理DPI切替、ACL非対応実媒体、実ディスク容量枯渇は未検証です。

tests/Privacy1110/Legacy119とAndroid104は過去の互換性試験入力で、正式アプリへ含めません。旧版やAndroidの開発領域を変更しません。
