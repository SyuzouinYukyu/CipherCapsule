using CipherCapsule.Core;
using CipherCapsule.UI;
using System.Diagnostics;
namespace CipherCapsule.Viewer;
public sealed class CopyTextBox : TextBox
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AllowCopy{get;set;}=true;
    protected override void WndProc(ref Message m){if(!AllowCopy&&m.Msg is 0x301 or 0x300 or 0x7b){m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
    protected override bool ProcessCmdKey(ref Message m,Keys key){if(!AllowCopy&&key is (Keys.Control|Keys.C) or (Keys.Control|Keys.Insert) or (Keys.Control|Keys.A) or (Keys.Control|Keys.X) or (Keys.Shift|Keys.F10))return true;return base.ProcessCmdKey(ref m,key);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!AllowCopy)SelectionLength=0;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!AllowCopy)SelectionLength=0;}
}
public sealed class ViewerForm : AppForm
{
    readonly Container.UnlockedCapsule capsule;readonly CopyTextBox text=new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=false,AccessibleName="復号したメッセージ"};
    readonly ListView files=new(){View=View.Details,FullRowSelect=true,MultiSelect=false,Dock=DockStyle.Fill,HideSelection=false,ShowItemToolTips=true,AccessibleName="暗号化された添付一覧"};
    readonly Label status=Label("添付は保存操作まで復号ファイルを書き出しません。");readonly Button save,saveAll,cancel;readonly ProgressBar bar=new(){Dock=DockStyle.Fill,Maximum=1000};readonly ToolTip tips=new();
    readonly SplitContainer split=new(){Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=9,Size=new Size(1000,600),Panel1MinSize=150,Panel2MinSize=100,SplitterDistance=410};
    HtmlView? html;CancellationTokenSource? cts;bool busy,closing,closePending;
    public ViewerForm(Container.UnlockedCapsule capsule)
    {
        this.capsule=capsule;Text="CipherCapsule — メッセージ閲覧";Size=new Size(1080,850);MinimumSize=new Size(800,650);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));
        if(capsule.Manifest.Kind==BodyKind.TEXT){text.AllowCopy=capsule.Manifest.AllowCopy;text.ShortcutsEnabled=capsule.Manifest.AllowCopy;text.Text=capsule.Manifest.Body;split.Panel1.Controls.Add(text);}
        else{html=new(hostZoom:true);split.Panel1.Controls.Add(html);Shown+=async(_,_)=>{try{await html.ShowHtml(capsule.Manifest.Body,UiSize,capsule.Manifest.AllowCopy);}catch(Exception ex){Error(this,ex);}};}
        files.Columns.Add("添付ファイル名",440);files.Columns.Add("サイズ",170);files.Columns.Add("番号",80);long bytes=0;
        for(int i=0;i<capsule.Manifest.Attachments.Count;i++){var a=capsule.Manifest.Attachments[i];bytes+=a.Size;files.Items.Add(new ListViewItem([a.Name,Display.Size(a.Size),(i+1).ToString()]){ToolTipText=$"{a.Name}\n{a.Size:N0} B"});}
        split.Panel2.Controls.Add(files);root.Controls.Add(split,0,0);save=Button("選択した添付を保存…",async(_,_)=>await Save());save.Enabled=false;cancel=Button("保存キャンセル",(_,_)=>cts?.Cancel());cancel.Enabled=false;
        saveAll=Button("すべての添付を保存…",async(_,_)=>await SaveAll());saveAll.Enabled=files.Items.Count>0;
        var count=Label($"添付 {files.Items.Count}件 / {Display.Size(bytes)}");tips.SetToolTip(count,$"{bytes:N0} B");
        var actions=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};actions.Controls.Add(Flow(count),0,0);actions.Controls.Add(Flow(save,saveAll,cancel,Button("閉じる",(_,_)=>Close())),0,1);root.Controls.Add(actions,0,1);
        root.Controls.Add(Flow(status),0,2);
        var footer=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));footer.Controls.Add(bar,0,0);
        var spec=new LinkLabel{Text="暗号アルゴリズム仕様",AutoSize=true,Anchor=AnchorStyles.Right,Margin=new Padding(10,4,4,4)};tips.SetToolTip(spec,"実装されている暗号方式・整合性確認・制限を表示します。");spec.LinkClicked+=(_,_)=>ShowSpecification();footer.Controls.Add(spec,1,0);root.Controls.Add(footer,0,3);Controls.Add(root);
        files.SelectedIndexChanged+=(_,_)=>save.Enabled=!busy&&files.SelectedIndices.Count==1;
        FormClosing+=async(_,e)=>{if(closing)return;e.Cancel=true;if(busy){cts?.Cancel();return;}if(closePending)return;closePending=true;text.Clear();if(html!=null&&!await html.CloseAsync())MessageBox.Show(this,"WebView2の終了後、一時領域の削除を完了できませんでした。完全消去は保証できません。\n"+html.CleanupFailure,"一時領域",MessageBoxButtons.OK,MessageBoxIcon.Warning);closing=true;Close();};
    }
    protected override void OnFontSizeChanged(){html?.SetDisplaySize(UiSize);}
    async Task Save()
    {
        if(busy||files.SelectedIndices.Count!=1)return;int i=files.SelectedIndices[0];var a=capsule.Manifest.Attachments[i];
        using var d=AttachmentNames.Dialog(a.Name);if(d.ShowDialog(this)!=DialogResult.OK)return;var watch=Stopwatch.StartNew();
        try{string dest=AttachmentNames.SinglePath(d.FileName,a.Name);bool overwrite=File.Exists(dest);
            if(overwrite&&MessageBox.Show(this,"既存のファイルを上書きしますか？\n"+dest,"上書き確認",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            AttachmentNames.ValidateTarget(dest);SetSaving(true);var p=new Progress<CapsuleProgress>(x=>{status.Text=x.Stage+$" {Display.Size(x.Done)} / {Display.Size(x.Total)}";bar.Value=(int)Math.Clamp(x.Done*1000/Math.Max(1,x.Total),0,1000);});await Task.Run(()=>capsule.Save(i,dest,overwrite,cts!.Token,p));watch.Stop();status.Text="添付を保存しました。";using var summary=AttachmentResults.Single($"添付を保存しました。\n\nファイル名：{Path.GetFileName(dest)}\n保存先：{dest}\nサイズ：{Display.Size(a.Size)} ({a.Size:N0} B)\n処理時間：{Display.Time(watch.Elapsed)}",dest);summary.ShowDialog(this);}
        catch(Exception ex){status.Text=ex is OperationCanceledException?"保存をキャンセルしました。":"添付を保存できませんでした。";Error(this,ex);}
        finally{SetSaving(false);}
    }
    void SetSaving(bool value){busy=value;files.Enabled=!value;save.Enabled=!value&&files.SelectedIndices.Count==1;saveAll.Enabled=!value&&files.Items.Count>0;cancel.Enabled=value;if(value)cts=new();else{cts?.Dispose();cts=null;}}
    async Task SaveAll()
    {
        if(busy||files.Items.Count==0)return;
        using var dialog=new FolderBrowserDialog{Description="すべての添付の保存先フォルダーを選択してください。",UseDescriptionForTitle=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{
            SetSaving(true);var refresh=Stopwatch.StartNew();string current="";int completed=-1;var progress=new Progress<BatchProgress>(p=>{if(current==p.Current&&completed==p.Completed&&p.Fraction<1&&refresh.ElapsedMilliseconds<100)return;refresh.Restart();current=p.Current;completed=p.Completed;status.Text=$"全{p.Total}件中 {p.Completed}件完了 / {p.Current} / {p.Fraction:P0} ({Display.Size(p.BytesDone)} / {Display.Size(p.BytesTotal)})";bar.Value=(int)Math.Clamp(p.Fraction*1000,0,1000);});
            var result=await Task.Run(()=>AttachmentBatch.Save(capsule,dialog.SelectedPath,cts!.Token,progress));
            status.Text=result.Reason;
            using var summary=AttachmentResults.Batch(result);summary.ShowDialog(this);
        }catch(Exception ex){status.Text="一括保存を完了できませんでした。";Error(this,ex);}
        finally{SetSaving(false);}
    }
    void ShowSpecification()
    {
        string details="本文・鍵：XChaCha20-Poly1305\r\n共通鍵長：256bit / nonce長：192bit / 認証タグ：128bit\r\nパスワード鍵導出：Argon2id v1.3 (ARGON2ID13)\r\nメモリ：256MiB / 演算パラメータ：3 / Salt：16byte\r\n鍵管理：暗号化ごとに新しいランダムDEK、パスワード由来KEKでDEKを暗号化\r\n用途別鍵：libsodium crypto_kdfによる本文・添付の鍵分離\r\n添付：crypto_secretstream_xchacha20poly1305、原則1MiBチャンク\r\n改ざん検出：マニフェストの認証、添付暗号文のSHA-256・構造検査、保存時の全チャンク認証とTAG_FINAL検証\r\n\r\nSHA-512：配布EXE全体の整合性確認用です。暗号方式そのものではなく、チェックサム単独では送信者の真正性を証明できません。\r\n短いパスワードは推測攻撃に弱いため、長いパスフレーズを推奨します。\r\n暗号化データの認証は送信者本人の証明とは異なります。\r\n\r\nコピー禁止は通常UIの制限です。スクリーンショット、OCR、外部カメラ、プロセスメモリ解析を防止するDRMではありません。\r\n表示・コピー・保存後の情報の完全消去は保証できません。";
        var version=typeof(ViewerForm).Assembly.GetName().Version!;details+=$"\r\n\r\nCipherCapsule v{version.Major}.{version.Minor}.{version.Build}\r\nGitHub：https://github.com/SyuzouinYukyu/CipherCapsule";
        using var f=new ReadOnlyDialog("暗号アルゴリズム仕様",details,new Size(920,650));f.ShowDialog(this);
    }
}
