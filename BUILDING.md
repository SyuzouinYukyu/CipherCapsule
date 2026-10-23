# CipherCapsule v1.1.7をソースからビルド

Windows 11 x64、.NET SDK 10.0.402、Python 3、Node.jsを使用。NuGet依存は固定版・ロックファイルを維持します。

リポジトリ直下で：
```text
python scripts/build_repository.py --stage --test-headless
```
固定libsodiumハッシュ・公式署名とTinyMCE配布物の完全性を検証し、Work/rebuild/<日時>/Sourceへ隔離コピーします。Viewerを先にRelease/win-x64、SelfContained、PublishSingleFile、圧縮有効で発行し、Builderへ埋め込みます。PublishTrimmed=false。配布済みEXEは置き換えません。結果は隔離コピーのWork/link117-stageです。

入力検証だけ：`python scripts/build_repository.py --check-inputs`

公開アダプターとリンク・フォルダ終了試験の最小ビルド確認：`python scripts/build_repository.py --test-only`

--test-headless（旧--testも同義）はtests/Link117の17件のみ。--test-onlyはViewer/BuilderをPublishせず同じ17件だけをコンパイル・実行します。実ブラウザー、実レジストリ、クリップボード、画面入力、ウィンドウ表示・前面化は行いません。古いGUI・診断ハーネスは参考ソースとして保全していますが、通常コマンドからは実行しません。一部は旧レイアウトと個別ダミーを要求し、現行GUIの合格判定には使いません。

v1.1.7の追加17件成功／0件失敗。継承した333件・68件・22件は本版の全件再試験結果ではありません。実リンクは利用者の手動確認済みです。フォルダ起動後の成功ダイアログ自動終了も利用者の手動確認済みです。他の新GUIは手動確認待ちです。バイナリ完全一致の再現可能ビルド認証は行っていません。

src/Shared/SpecificationDialog.csが読み取り専用RichTextBoxのURLをリンク化し、RepositoryLink.csが固定HTTPSだけをOSの既定ブラウザーへ渡します。Builderの暗号化成功結果だけ、フォルダ起動成功時に閉じます。その他の結果ダイアログの動作は維持します。CCAPと自己復号EXEは同じ仕様画面を使います。

ライセンス・対応ソースはLICENSE、THIRD_PARTY_NOTICES.md、licenses、vendor内に保持。固定パスワード・本文は公開用ダミーで、個人INI・生成試験EXE・内部記録は含みません。TinyMCEの編集可能な上流ソースとビルド資料もvendor内に提供しています。
