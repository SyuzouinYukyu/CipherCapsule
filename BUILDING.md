# Windows v1.1.10 ビルド

公開リポジトリ直下で `python scripts/build_repository.py --stage` を実行します。`--check-inputs` は固定依存資産の入力検証だけを行います。.NET SDK 10.0.402、Windows 11 x64、固定NuGet版とロックファイルを使います。既存のvendor/libsodium.dllを再利用し、libsodium・Androidアプリは再ビルドしません。

ViewerをRelease/win-x64/SelfContained/PublishSingleFile/PublishTrimmed=falseで圧縮Publishし、templates/CipherCapsule_Viewer.exeを更新してからBuilderをPublishします。Builderの出力はWork/stage1110/Builder/CipherCapsule_v1.1.10.exeです。既存ステージの上書きは拒否します。

限定試験：`python scripts/test1110.py`。GUIや実ユーザーのレジストリを操作しない隔離ストア、メモリ内Armor、通常ファイル経路、旧版・Android互換Coreを検証します。テスト用のジャンクション作成には実行環境の許可が必要です。既存の全件GUI試験は呼び出しません。

tests/Privacy1110/Legacy119は参照用v1.1.9 Coreのコピー、Android104は公開コミットを固定した未改変のAndroid Coreです。android-source-origin.jsonにGit blobを記録しています。これらはWindowsホストで互換性を調べる試験入力で、正式アプリには含みません。旧版開発ディレクトリへの出力は行いません。

従来の依存関係・第三者ライセンスはBUILDING_PREVIOUS_REFERENCE.mdを参照してください。実デスクトップへ入力・フォーカスを送る検証は行いません。開発時の実機GUI結果は上記の記録に含まれます。
