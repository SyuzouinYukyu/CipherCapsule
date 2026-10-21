# 第三者コンポーネントと対応ソース

CipherCapsule自身のソースコードはGPL-2.0-or-laterで提供します。本文は`LICENSE`、プロジェクトの著作権表示と条件は`licenses/CipherCapsule-license.txt`を参照してください。
第三者コンポーネントを一律に再ライセンスするものではありません。

| コンポーネント | 固定版 | ライセンス・同梱資料 |
|---|---|---|
| TinyMCE Community | 8.10.0 | GPLv2以降。`licenses/TinyMCE-license.txt`、`licenses/GPL-2.0.txt` |
| TinyMCE日本語言語パック | tinymce-i18n 26.10.19 | パッケージ独自コードはMIT、言語ファイルは対応するTinyMCEの条件。`licenses/TinyMCE-i18n-license.txt` |
| libsodium | 1.0.22-stable、2026-10-15署名スナップショット | ISC。`licenses/libsodium-LICENSE.txt` |
| AngleSharp | 1.8.5 | MIT。`licenses/AngleSharp-LICENSE.txt` |
| Microsoft.Web.WebView2 SDK | 1.0.4258.31 | 同梱のMicrosoft再配布条件。`licenses/WebView2-LICENSE.txt`、`licenses/WebView2-NOTICE.txt` |
| .NET Runtime / Windows Desktop Runtime | 10.0.13 | MITおよび第三者通知。`licenses/NETCore-LICENSE.txt`、`licenses/NETCore-THIRD-PARTY-NOTICES.TXT`、`licenses/WindowsDesktop-LICENSE.txt` |

TinyMCEは`license_key: 'gpl'`で自己ホストしています。商用プラグイン・クラウドサービスは使用していません。
上流の編集可能なTypeScriptソース、ビルドスクリプト、依存管理ファイルは`vendor/tinymce-source-8.10.0.tar.gz`に含まれます。
実際の埋込みに使用するnpm配布物、日本語パックおよび完全性固定値も`vendor`内にあります。
[TinyMCE公式のGPL設定説明](https://www.tiny.cloud/docs/tinymce/latest/license-key/)も参照できます。

libsodiumの公式署名付きアーカイブと`.minisig`を提供しています。`scripts/prepare.py`と`verify-minisign.cjs`が固定ハッシュと公式署名を検証します。
NuGet依存関係は各プロジェクトの`packages.lock.json`と`global.json`で管理しています。
WebView2 Runtime自体はEXEに同梱せず、Microsoftから導入したRuntimeを利用します。

このタグのアプリケーションソース、ローカル編集統合、ビルド入力、上流対応ソースは、GitHubのタグ付きソースアーカイブから一括取得できます。
Releaseには同じライセンス資料のZIPも添付しています。これらの著作権表示と条件を再配布時にも保持してください。

`licenses/CipherCapsule-license.txt`内の`Source`は元の開発フォルダー構成を指します。このリポジトリでは同じ内容を直下へ配置し、`BUILDING.md`の補助スクリプトで元の構成へコピーしてビルドします。
