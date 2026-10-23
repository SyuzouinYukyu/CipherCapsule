using CipherCapsule.Core;
using CipherCapsule.UI;
using System.Reflection;
using System.Diagnostics;
namespace CipherCapsule.Builder;
public sealed class BuilderForm : AppForm
{
    readonly CheckBox selfExtracting=new(){Text="自己展開アーカイブの作成",Checked=true,AutoSize=true,Margin=new Padding(6),AccessibleName="自己展開アーカイブの作成"};
    readonly Button openCcap;
    bool opening;readonly HashSet<Control> dropControls=[];
    readonly ComboBox mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130,AccessibleName="本文形式"};
    readonly TextBox editor=new(){Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AcceptsReturn=true,AcceptsTab=true,MaxLength=Limits.Body,AccessibleName="メッセージ本文",AllowDrop=true};
    readonly HtmlEditor htmlEditor=new(){Visible=false};readonly Panel bodyArea=new(){Dock=DockStyle.Fill};
    readonly ListView files=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false,AllowDrop=true,ShowItemToolTips=true,AccessibleName="添付ファイル"};
    readonly PasswordPanel password=new();
    readonly CheckBox allowCopy=new(){Text="テキストのコピーを許可する",Checked=true,AutoSize=true,Margin=new Padding(6)};
    readonly CheckBox checksum=new(){Text="SHA-512チェックサムファイルを出力する",Checked=true,AutoSize=true,Margin=new Padding(6)};
    readonly CheckBox checksum256=new(){Text="SHA-256チェックサムファイルを出力する",Checked=false,AutoSize=true,Margin=new Padding(6)};
    readonly TextBox result=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AccessibleName="生成結果"};
    readonly ProgressBar bar=new(){Dock=DockStyle.Fill,Maximum=1000};
    readonly Label status=Label("本文とパスワードを入力し、「暗号化する」で保存先を確定してください。"),total=Label("添付 0件 / 0 B");
    readonly Button generate,cancel;readonly Panel editArea=new(){Dock=DockStyle.Fill};readonly List<string> paths=[];readonly ToolTip tips=new();
    readonly TableLayoutPanel root=new(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};
    readonly TableLayoutPanel config=new(){Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1};CancellationTokenSource? cts;bool busy,closing,closePending;
    string lastOutputDirectory=Settings.Documents;
    public BuilderForm()
    {
        Text="CipherCapsule v1.1.7";MinimumSize=new Size(900,920);ClientSize=new Size(1240,980);
        var settings=Settings.Load();SetSize(settings.Font);Size=new Size(Math.Max(900,settings.Width),Math.Max(960,settings.Height));
        lastOutputDirectory=settings.LastOutputDirectory;
        password.SetRevealed(settings.PasswordRevealed);
        var rect=new Rectangle(settings.X,settings.Y,Width,Height);if(Screen.AllScreens.Any(s=>Rectangle.Intersect(s.WorkingArea,rect).Width>=200&&Rectangle.Intersect(s.WorkingArea,rect).Height>=100)){StartPosition=FormStartPosition.Manual;Location=rect.Location;}
        mode.Items.AddRange(["TEXT","HTML"]);mode.SelectedIndex=settings.Kind;selfExtracting.Checked=ArchiveOutputSelection.IsSelfExtracting(settings.OutputFormat);
        files.Columns.Add("ファイル名",400);files.Columns.Add("サイズ",170);files.Columns.Add("番号",80);
        var message=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};message.RowStyles.Add(new(SizeType.AutoSize));message.RowStyles.Add(new(SizeType.Percent,100));
        message.Controls.Add(Flow(Label("メッセージ本文"),mode,Button("本文を読込…",async(_,_)=>await ReadBodyDialog()),Button("HTMLプレビュー",async(_,_)=>await Preview()),Button("ライセンス・注意事項",(_,_)=>ShowNotices())),0,0);
        bodyArea.Controls.Add(editor);bodyArea.Controls.Add(htmlEditor);message.Controls.Add(bodyArea,0,1);
        var attachments=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};attachments.RowStyles.Add(new(SizeType.AutoSize));attachments.RowStyles.Add(new(SizeType.Percent,100));attachments.RowStyles.Add(new(SizeType.AutoSize));
        attachments.Controls.Add(Flow(Label("添付ファイル"),Button("追加…",(_,_)=>AddDialog()),Button("貼り付け",(_,_)=>PasteFiles()),Button("削除",(_,_)=>RemoveFiles()),Button("全削除",(_,_)=>{paths.Clear();RefreshFiles();})),0,0);attachments.Controls.Add(files,0,1);attachments.Controls.Add(total,0,2);
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=8,Size=new Size(1200,580),Panel1MinSize=180,Panel2MinSize=170,SplitterDistance=370};split.Panel1.Controls.Add(message);split.Panel2.Controls.Add(attachments);editArea.Controls.Add(split);Shown+=(_,_)=>{int height=split.ClientSize.Height;if(height>358)split.SplitterDistance=Math.Clamp((int)(height*0.62),180,height-178);};
        generate=Button("暗号化する",async(_,_)=>await Generate());cancel=Button("キャンセル",(_,_)=>cts?.Cancel());cancel.Enabled=false;
        generate.Padding=cancel.Padding=new Padding(12,8,12,8);generate.Margin=new Padding(0,4,10,4);cancel.Margin=new Padding(0,4,0,4);SizeActionButtons();
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,AutoSize=true,Margin=new Padding(0)};actions.ColumnStyles.Add(new(SizeType.Percent,100));actions.ColumnStyles.Add(new(SizeType.AutoSize));actions.ColumnStyles.Add(new(SizeType.AutoSize));generate.Anchor=cancel.Anchor=AnchorStyles.Right;actions.Controls.Add(generate,1,0);actions.Controls.Add(cancel,2,0);
        openCcap=Button("暗号化ファイルを開く…",async(_,_)=>await OpenCcapDialog());
        config.Controls.Add(Flow(selfExtracting));config.Controls.Add(Flow(openCcap));
        config.Controls.Add(password);config.Controls.Add(Label("空欄不可・最大256文字。空白も保持します。安全のため長いパスフレーズを推奨します。"));var sums=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Margin=new Padding(0)};sums.Controls.AddRange([checksum,checksum256]);config.Controls.Add(Flow(allowCopy,sums));config.Controls.Add(actions);
        root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Absolute,94));root.RowStyles.Add(new(SizeType.AutoSize));root.Controls.Add(editArea,0,0);root.Controls.Add(config,0,1);root.Controls.Add(result,0,2);
        var progress=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1};progress.Controls.Add(status);progress.Controls.Add(bar);root.Controls.Add(progress,0,3);Controls.Add(root);
        tips.SetToolTip(allowCopy,"コピー禁止は通常のUI操作の制限です。スクリーンショットやOCR、メモリ解析を防ぐDRMではありません。");tips.SetToolTip(checksum,"SHA-512は完成ファイル全体の整合性確認用です。送信者本人の証明にはなりません。");tips.SetToolTip(checksum256,"SHA-256は完成ファイル全体のチェックサムを.sha256へ出力します。送信者本人の証明にはなりません。");tips.SetToolTip(selfExtracting,"ON：自己復号EXE（.exe）を作成します。OFF：CCAP（.ccap）を作成します。");
        password.ValidityChanged+=(_,_)=>UpdateRun();mode.SelectedIndexChanged+=(_,_)=>SwitchMode();
        AttachFileDrops(this);
        htmlEditor.FileDropRequested=values=>HandleFileDrop(values,FileDropArea.Other);
        FormClosing+=async(_,e)=>{if(closing)return;e.Cancel=true;if(busy){cts?.Cancel();return;}if(closePending)return;closePending=true;bool cleaned=await htmlEditor.CloseAsync();if(!cleaned)MessageBox.Show(this,"WebView2の一時領域を削除できませんでした。完全消去は保証できません。\n"+htmlEditor.CleanupFailure,"一時領域",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            SaveSettings();
            password.ClearSecrets();editor.Clear();closing=true;Close();};
        SwitchMode();UpdateRun();SizeChecksumRow();
    }
    void SwitchMode(){bool html=mode.SelectedIndex==1;editor.Visible=!html;htmlEditor.Visible=html;if(html)htmlEditor.BringToFront();else editor.BringToFront();}
    void UpdateRun()=>generate.Enabled=!busy&&!opening&&password.CanRun;
    void SetBusy(bool value){busy=value;selfExtracting.Enabled=!value;openCcap.Enabled=!value;editArea.Enabled=!value;password.Enabled=!value;allowCopy.Enabled=!value;checksum.Enabled=!value;checksum256.Enabled=!value;generate.Enabled=!value&&password.CanRun;cancel.Enabled=value;}
    bool DropBlocked=>busy||opening||closing||closePending||IsDisposed||Disposing||!Enabled;
    FileDropArea DropArea(Control control)=>ReferenceEquals(control,editor)?FileDropArea.TextBody:ReferenceEquals(control,files)?FileDropArea.Attachments:FileDropArea.Other;
    static string[] DroppedFiles(IDataObject? data){try{return data?.GetData(DataFormats.FileDrop,false) as string[]??[];}catch{return [];}}
    void AttachFileDrops(Control control)
    {
        if(!dropControls.Add(control))return;
        // WebView2 has its own native drop surface; its trusted editor bridge is used instead.
        if(control is not Microsoft.Web.WebView2.WinForms.WebView2){
            control.AllowDrop=true;
            void CheckDrag(object? sender,DragEventArgs e){e.Effect=(e.AllowedEffect&DragDropEffects.Copy)!=0&&FileDropRouting.Decide(DroppedFiles(e.Data),DropArea(control),DropBlocked)!=FileDropAction.Ignore?DragDropEffects.Copy:DragDropEffects.None;}
            control.DragEnter+=CheckDrag;control.DragOver+=CheckDrag;
            control.DragDrop+=async(_,e)=>{if((e.AllowedEffect&DragDropEffects.Copy)!=0)await HandleFileDrop(DroppedFiles(e.Data),DropArea(control));};
        }
        control.ControlAdded+=(_,e)=>{if(e.Control!=null)AttachFileDrops(e.Control);};
        foreach(Control child in control.Controls)AttachFileDrops(child);
    }
    async Task HandleFileDrop(string[] values,FileDropArea area)
    {
        switch(FileDropRouting.Decide(values,area,DropBlocked)){
            case FileDropAction.OpenCcap:await OpenCcap(values);break;
            case FileDropAction.ReadBody:await ReadBody(values[0]);break;
            case FileDropAction.AddAttachments:AddFiles(values);break;
        }
    }
    protected override void OnFontSizeChanged(){SizeActionButtons();SizeChecksumRow();}
    void SizeChecksumRow(){if(checksum==null||checksum256==null)return;int width=checksum.GetPreferredSize(Size.Empty).Width+checksum.Margin.Horizontal+checksum256.GetPreferredSize(Size.Empty).Width+checksum256.Margin.Horizontal+(int)Math.Ceiling(72*DeviceDpi/96f);MinimumSize=new Size(Math.Max(900,width),MinimumSize.Height);}
    protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);SizeChecksumRow();}
    void SizeActionButtons(){if(generate==null||cancel==null)return;float scale=UiSize/12f*DeviceDpi/96f;generate.MinimumSize=new Size((int)Math.Ceiling(180*scale),(int)Math.Ceiling(52*scale));cancel.MinimumSize=new Size((int)Math.Ceiling(150*scale),(int)Math.Ceiling(52*scale));}
    void AddDialog(){using var d=new OpenFileDialog{Multiselect=true,Title="添付ファイルを追加"};if(d.ShowDialog(this)==DialogResult.OK)AddFiles(d.FileNames);}
    void AddFiles(IEnumerable<string> values){try{var all=paths.Concat(values.Select(Path.GetFullPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();Limits.Inputs(all);paths.Clear();paths.AddRange(all);RefreshFiles();}catch(Exception ex){Error(this,ex);}}
    void RefreshFiles(){files.Items.Clear();for(int i=0;i<paths.Count;i++){var f=new FileInfo(paths[i]);files.Items.Add(new ListViewItem([f.Name,f.Exists?Display.Size(f.Length):"見つかりません",(i+1).ToString()]){ToolTipText=paths[i]+(f.Exists?$"\n{f.Length:N0} B":"")});}long bytes=paths.Where(File.Exists).Sum(p=>new FileInfo(p).Length);total.Text=$"添付 {paths.Count}件 / {Display.Size(bytes)}";tips.SetToolTip(total,$"{bytes:N0} B");}
    void RemoveFiles(){foreach(int i in files.SelectedIndices.Cast<int>().OrderDescending())paths.RemoveAt(i);RefreshFiles();}
    void PasteFiles(){try{if(Clipboard.ContainsFileDropList())AddFiles(Clipboard.GetFileDropList().Cast<string>());else MessageBox.Show(this,"Explorerでコピーしたファイルを貼り付けられます。");}catch(Exception ex){Error(this,ex);}}
    async Task ReadBodyDialog(){using var d=new OpenFileDialog{Filter="本文 (*.txt;*.html;*.htm)|*.txt;*.html;*.htm|すべてのファイル|*.*"};if(d.ShowDialog(this)==DialogResult.OK)await ReadBody(d.FileName);}
    async Task ReadBody(string path){try{if(new FileInfo(path).Length>Limits.Body)throw new ArgumentException("本文はUTF-8換算8MiB以内にしてください。");string value=File.ReadAllText(path,Limits.Utf8);Limits.BodyText(value);bool html=Path.GetExtension(path).ToLowerInvariant() is ".html" or ".htm";string current=html?await htmlEditor.GetContent():editor.Text;if(current.Length>0&&MessageBox.Show(this,"編集中の本文を読込内容で置き換えますか？","本文読込",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;mode.SelectedIndex=html?1:0;if(html)await htmlEditor.SetContent(value);else editor.Text=value;}catch(Exception ex){Error(this,ex);}}
    async Task Preview(){if(mode.SelectedIndex!=1){MessageBox.Show(this,"HTMLモードを選択してください。");return;}string body=await htmlEditor.GetContent();using var f=new AppForm{Text="CipherCapsule — HTMLプレビュー",Size=new Size(1000,740)};using var view=new HtmlView();f.Controls.Add(view);bool done=false,closePending=false;f.Shown+=async(_,_)=>{try{await view.ShowHtml(body,UiSize);}catch(Exception ex){Error(f,ex);}};f.FormClosing+=async(_,e)=>{if(done)return;e.Cancel=true;if(closePending)return;closePending=true;if(!await view.CloseAsync())MessageBox.Show(f,"WebView2一時領域の削除を完了できませんでした。\n"+view.CleanupFailure);done=true;f.Close();};await PostModal(this,()=>f.ShowDialog(this));}
    async Task Generate()
    {
        if(busy||opening)return;byte[]? p=null;var watch=Stopwatch.StartNew();string stage="入力の検証";string? workFolder=null;HashSet<string> previousTemporary=[];
        try{
            SetBusy(true);p=password.Take(true);BodyKind kind=(BodyKind)mode.SelectedIndex;string body=kind==BodyKind.HTML?await htmlEditor.GetContent():editor.Text;Limits.BodyText(body);Limits.Inputs(paths);
            if(kind==BodyKind.HTML){body=SafeHtml.ValidateEditor(body,out bool warning);if(warning&&MessageBox.Show(this,"HTMLに不正な構造または危険な要素があります。安全に正規化した本文で暗号化しますか？","HTML安全性",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes){status.Text="暗号化をキャンセルしました。入力内容は保持しています。";return;}}
            using var d=CreateSaveDialog();
            if(d.ShowDialog(this)!=DialogResult.OK){status.Text="保存先の選択をキャンセルしました。入力内容は保持しています。";return;}
            var format=ArchiveOutputSelection.FromSelfExtracting(selfExtracting.Checked);string dest=OutputSelection.FinalPath(d.FileName,format);
            if(File.Exists(dest)&&MessageBox.Show(this,"既存のファイルを上書きしますか？\n"+dest,"上書き確認",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){status.Text="暗号化をキャンセルしました。";return;}
            if(string.Equals(dest,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("CipherCapsule本体には上書きできません。");
            foreach(string suffix in new[]{checksum.Checked?".sha512":null,checksum256.Checked?".sha256":null}.OfType<string>())if(File.Exists(dest+suffix)&&MessageBox.Show(this,"既存のチェックサムファイルを置き換えますか？\n"+dest+suffix,"上書き確認",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){status.Text="暗号化をキャンセルしました。";return;}
            workFolder=Path.GetDirectoryName(dest)!;previousTemporary=Directory.EnumerateFiles(workFolder,".cc-*.tmp").ToHashSet(StringComparer.OrdinalIgnoreCase);cts=new();result.Clear();var progress=new Progress<CapsuleProgress>(x=>{stage=x.Stage;status.Text=x.Stage+(x.Total>1?$" {Display.Size(x.Done)} / {Display.Size(x.Total)}":"");bar.Value=(int)Math.Clamp(x.Done*1000/Math.Max(1,x.Total),0,1000);});
            string[] inputs=paths.ToArray();byte[] pw=p;bool sum=checksum.Checked,sum256=checksum256.Checked,copy=allowCopy.Checked;stage="暗号化";
            var package=await Task.Run(()=>{using var template=format==OutputFormat.CCAP?Stream.Null:Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewerTemplate")??throw new IOException();return PackageWriter.Generate(template,dest,body,kind,inputs,pw,sum,true,cts.Token,progress,copy,sum256);});
            lastOutputDirectory=Path.GetDirectoryName(package.Path)!;SaveSettings();
            watch.Stop();string message=$"暗号化が完了しました。\r\n出力形式：{(format==OutputFormat.CCAP?"CipherCapsule形式 (.ccap)":"自己復号EXE (.exe)")}\r\n\r\nファイル名：{Path.GetFileName(package.Path)}\r\n保存先：{Path.GetDirectoryName(package.Path)}\r\nフルパス：{package.Path}\r\nファイルサイズ：{Display.Size(package.Size)} ({package.Size:N0} B)\r\n処理時間：{Display.Time(watch.Elapsed)}\r\nSHA-512：{(package.Sha512!=null?"正常に生成しました。":"出力しない設定です。")}\r\nSHA-256チェックサムファイル：{(package.Sha256FileWritten?"正常に生成しました。":"出力しない設定です。")}\r\nSHA-256：{package.Sha256}";
            result.Text=message;status.Text="暗号化完了。パスワードと編集内容はこのセッション内で保持しています。";ResultDialog(message,"暗号化成功",package.Path);
        }
        catch(Exception ex){watch.Stop();string explanation=ex is OperationCanceledException?"処理をキャンセルしました。":ex is ArgumentException?ex.Message:ex is UnauthorizedAccessException?"アクセスが拒否されました。保存先の書込み権限とファイルの使用状況を確認してください。":ex is IOException?"ファイルを処理できません。空き容量・ファイルの使用状況・入力ファイルの変更を確認してください。"+ (ex.Message.StartsWith("出力の復元")?"\r\n"+ex.Message:""):"暗号化を完了できません。入力内容と実行環境を確認してください。";
            string cleanup="一時ファイルを作成する前に終了しました。";if(workFolder!=null)try{var remaining=Directory.EnumerateFiles(workFolder,".cc-*.tmp").Where(x=>!previousTemporary.Contains(x)).ToArray();cleanup=remaining.Length==0?"一時ファイルの後始末：残存なしを確認しました。":"一時・退避ファイルが残っています。保管して手動確認してください："+string.Join("、",remaining);}catch{cleanup="一時ファイルの後始末結果を確認できません。保存先を手動確認してください。";}
            string message=$"{(ex is OperationCanceledException?"キャンセル":"暗号化失敗")}\r\n処理：{stage}\r\n原因・対処：{explanation}\r\n経過時間：{Display.Time(watch.Elapsed)}\r\n{cleanup}\r\n入力済みパスワード・編集内容は保持しています。";result.Text=message;status.Text="処理を完了できませんでした。入力内容は保持しています。";ResultDialog(message,"暗号化結果");}
        finally{Sodium.Wipe(p);cts?.Dispose();cts=null;SetBusy(false);}
    }
    SaveFileDialog CreateSaveDialog()=>OutputSelection.Dialog(ArchiveOutputSelection.FromSelfExtracting(selfExtracting.Checked),lastOutputDirectory);
    async Task OpenCcapDialog()
    {
        if(busy||opening)return;
        using var d=new OpenFileDialog{Filter="CipherCapsuleファイル (*.ccap)|*.ccap",Multiselect=false,Title="暗号化ファイルを開く"};
        if(d.ShowDialog(this)==DialogResult.OK)await OpenCcap(d.FileNames);
    }
    public async Task OpenCcap(IReadOnlyList<string> names)
    {
        if(busy||opening)return;
        try{
            string path=CcapFiles.ValidateRequest(names);opening=true;openCcap.Enabled=false;UpdateRun();
            await PostModal(this,()=>{
                using var authentication=new PasswordDialog(path,ccapOnly:true);
                if(authentication.ShowDialog(this)!=DialogResult.OK||authentication.Capsule==null)return;
                using var capsule=authentication.Capsule;
                using var viewer=new CipherCapsule.Viewer.ViewerForm(capsule);viewer.SetSize(UiSize);viewer.ShowDialog(this);
            });
        }catch(Exception ex){Error(this,ex);}
        finally{opening=false;openCcap.Enabled=!busy;UpdateRun();}
    }

    bool SaveSettings()
    {
        try{var b=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;new Settings{X=b.X,Y=b.Y,Width=b.Width,Height=b.Height,Font=UiSize,Kind=mode.SelectedIndex,OutputFormat=ArchiveOutputSelection.FromSelfExtracting(selfExtracting.Checked),PasswordRevealed=password.Revealed,LastOutputDirectory=lastOutputDirectory}.Save();return true;}
        catch{MessageBox.Show(this,"設定を保存できません。EXE配置先の書込み権限を確認してください。完成した暗号化EXEと現在の入力内容は保持しています。","設定保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
    }
    static ReadOnlyDialog CreateResultDialog(string message,string title,string? completedOutputPath=null)
    {
        var f=new ReadOnlyDialog(title,message,new Size(900,520),true);
        if(completedOutputPath!=null){
            var open=Button("保存先フォルダを開く",(_,_)=>{string? warning=OutputFolder.OpenAndClose(completedOutputPath,f.Close);if(warning!=null)MessageBox.Show(f,warning,"保存先フォルダ",MessageBoxButtons.OK,MessageBoxIcon.Warning);});
            var group=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Anchor=AnchorStyles.None,Margin=new Padding(0)};
            var row=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0)};
            row.ColumnStyles.Add(new(SizeType.Percent,50));row.ColumnStyles.Add(new(SizeType.AutoSize));row.ColumnStyles.Add(new(SizeType.Percent,50));
            var layout=f.Controls.OfType<TableLayoutPanel>().Single();layout.Controls.Remove(f.CloseButton);
            group.Controls.Add(open);group.Controls.Add(f.CloseButton);row.Controls.Add(group,1,0);layout.Controls.Add(row,0,1);
            void SizeButtons(){int dpi=f.DeviceDpi;int height=(int)Math.Ceiling(40*Math.Max(1,f.UiSize/12f)*dpi/96f);open.MinimumSize=new Size(0,height);f.CloseButton.MinimumSize=new Size(0,height);open.Margin=new Padding(0,0,(int)Math.Ceiling(12*dpi/96f),0);f.CloseButton.Margin=new Padding(0);}
            SizeButtons();f.FontChanged+=(_,_)=>SizeButtons();f.DpiChanged+=(_,_)=>SizeButtons();
            f.Content.Select(0,0);f.ActiveControl=f.CloseButton;
        }
        return f;
    }
    void ResultDialog(string message,string title,string? completedOutputPath=null){using var f=CreateResultDialog(message,title,completedOutputPath);f.ShowDialog(this);}
    void ShowNotices(){var b=new System.Text.StringBuilder();var a=Assembly.GetExecutingAssembly();foreach(string name in a.GetManifestResourceNames().Where(n=>n.StartsWith("License.")).Order()){using var s=a.GetManifestResourceStream(name)!;using var r=new StreamReader(s);b.AppendLine(name[8..]).AppendLine(r.ReadToEnd()).AppendLine();}ResultDialog(b.ToString(),"CipherCapsule — ライセンス・注意事項");}
}
