using CipherCapsule.Core;
using CipherCapsule.UI;
using System.Reflection;
using System.Diagnostics;
namespace CipherCapsule.Builder;
public sealed class BuilderForm : AppForm
{
    readonly ComboBox mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130,AccessibleName="本文形式"};
    readonly TextBox editor=new(){Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AcceptsReturn=true,AcceptsTab=true,MaxLength=Limits.Body,AccessibleName="メッセージ本文",AllowDrop=true};
    readonly HtmlEditor htmlEditor=new(){Visible=false};readonly Panel bodyArea=new(){Dock=DockStyle.Fill};
    readonly ListView files=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false,AllowDrop=true,ShowItemToolTips=true,AccessibleName="添付ファイル"};
    readonly PasswordPanel password=new();readonly TextBox output=new(){Dock=DockStyle.Fill,AccessibleName="出力EXE保存先候補",AllowDrop=true};
    readonly CheckBox allowCopy=new(){Text="テキストのコピーを許可する",Checked=true,AutoSize=true,Margin=new Padding(6)};
    readonly CheckBox checksum=new(){Text="SHA-512チェックサムファイルを出力する",Checked=true,AutoSize=true,Margin=new Padding(6)};
    readonly TextBox result=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AccessibleName="生成結果"};
    readonly ProgressBar bar=new(){Dock=DockStyle.Fill,Maximum=1000};
    readonly Label status=Label("本文とパスワードを入力し、「暗号化する」で保存先を確定してください。"),total=Label("添付 0件 / 0 B");
    readonly Button generate,cancel,outputPaste;readonly Panel editArea=new(){Dock=DockStyle.Fill};readonly List<string> paths=[];readonly ToolTip tips=new();
    readonly TableLayoutPanel root=new(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};
    readonly TableLayoutPanel config=new(){Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1};CancellationTokenSource? cts;bool busy,closing,closePending;
    public BuilderForm()
    {
        Text="CipherCapsule v1.1.1";MinimumSize=new Size(900,920);ClientSize=new Size(1240,980);
        var settings=Settings.Load();SetSize(settings.Font);Size=new Size(Math.Max(900,settings.Width),Math.Max(960,settings.Height));
        var rect=new Rectangle(settings.X,settings.Y,Width,Height);if(Screen.AllScreens.Any(s=>Rectangle.Intersect(s.WorkingArea,rect).Width>=200&&Rectangle.Intersect(s.WorkingArea,rect).Height>=100)){StartPosition=FormStartPosition.Manual;Location=rect.Location;}
        mode.Items.AddRange(["TEXT","HTML"]);mode.SelectedIndex=settings.Kind;
        files.Columns.Add("ファイル名",400);files.Columns.Add("サイズ",170);files.Columns.Add("番号",80);
        var message=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};message.RowStyles.Add(new(SizeType.AutoSize));message.RowStyles.Add(new(SizeType.Percent,100));
        message.Controls.Add(Flow(Label("メッセージ本文"),mode,Button("本文を読込…",async(_,_)=>await ReadBodyDialog()),Button("HTMLプレビュー",async(_,_)=>await Preview()),Button("ライセンス・注意事項",(_,_)=>ShowNotices())),0,0);
        bodyArea.Controls.Add(editor);bodyArea.Controls.Add(htmlEditor);message.Controls.Add(bodyArea,0,1);
        var attachments=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};attachments.RowStyles.Add(new(SizeType.AutoSize));attachments.RowStyles.Add(new(SizeType.Percent,100));attachments.RowStyles.Add(new(SizeType.AutoSize));
        attachments.Controls.Add(Flow(Label("添付ファイル"),Button("追加…",(_,_)=>AddDialog()),Button("貼り付け",(_,_)=>PasteFiles()),Button("削除",(_,_)=>RemoveFiles()),Button("全削除",(_,_)=>{paths.Clear();RefreshFiles();})),0,0);attachments.Controls.Add(files,0,1);attachments.Controls.Add(total,0,2);
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=8,Size=new Size(1200,580),Panel1MinSize=180,Panel2MinSize=170,SplitterDistance=370};split.Panel1.Controls.Add(message);split.Panel2.Controls.Add(attachments);editArea.Controls.Add(split);Shown+=(_,_)=>{int height=split.ClientSize.Height;if(height>358)split.SplitterDistance=Math.Clamp((int)(height*0.62),180,height-178);};
        generate=Button("暗号化する",async(_,_)=>await Generate());cancel=Button("キャンセル",(_,_)=>cts?.Cancel());cancel.Enabled=false;
        var outRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,AutoSize=true};outRow.ColumnStyles.Add(new(SizeType.AutoSize));outRow.ColumnStyles.Add(new(SizeType.Percent,100));outRow.ColumnStyles.Add(new(SizeType.AutoSize));outRow.ColumnStyles.Add(new(SizeType.AutoSize));outRow.ColumnStyles.Add(new(SizeType.AutoSize));
        outputPaste=Button("貼り付け",(_,_)=>PasteOutput());outRow.Controls.Add(Label("EXE出力先候補"),0,0);outRow.Controls.Add(output,1,0);outRow.Controls.Add(outputPaste,2,0);outRow.Controls.Add(generate,3,0);outRow.Controls.Add(cancel,4,0);
        config.Controls.Add(password);config.Controls.Add(Label("空欄不可・最大256文字。空白も保持します。安全のため長いパスフレーズを推奨します。"));config.Controls.Add(Flow(allowCopy,checksum));config.Controls.Add(outRow);
        root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Absolute,94));root.RowStyles.Add(new(SizeType.AutoSize));root.Controls.Add(editArea,0,0);root.Controls.Add(config,0,1);root.Controls.Add(result,0,2);
        var progress=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1};progress.Controls.Add(status);progress.Controls.Add(bar);root.Controls.Add(progress,0,3);Controls.Add(root);
        tips.SetToolTip(allowCopy,"コピー禁止は通常のUI操作の制限です。スクリーンショットやOCR、メモリ解析を防ぐDRMではありません。");tips.SetToolTip(checksum,"SHA-512はEXE全体の整合性確認用です。送信者本人の証明にはなりません。");
        password.ValidityChanged+=(_,_)=>UpdateRun();mode.SelectedIndexChanged+=(_,_)=>SwitchMode();output.TextChanged+=(_,_)=>tips.SetToolTip(output,output.Text);
        editor.DragEnter+=DragFiles;files.DragEnter+=DragFiles;output.DragEnter+=DragFiles;
        editor.DragDrop+=async(_,e)=>{if(!busy&&e.Data?.GetData(DataFormats.FileDrop) is string[] p&&p.Length==1)await ReadBody(p[0]);};
        files.DragDrop+=(_,e)=>{if(!busy&&e.Data?.GetData(DataFormats.FileDrop) is string[] p)AddFiles(p);};
        output.DragDrop+=(_,e)=>{if(!busy&&e.Data?.GetData(DataFormats.FileDrop) is string[] p){try{if(p.Length!=1)throw new ArgumentException("出力先候補は1つだけ指定してください。");output.Text=Display.OutputCandidate(p[0]);}catch(Exception ex){Error(this,ex);}}};
        FormClosing+=async(_,e)=>{if(closing)return;e.Cancel=true;if(busy){cts?.Cancel();return;}if(closePending)return;closePending=true;bool cleaned=await htmlEditor.CloseAsync();if(!cleaned)MessageBox.Show(this,"WebView2の一時領域を削除できませんでした。完全消去は保証できません。\n"+htmlEditor.CleanupFailure,"一時領域",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            try{var b=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;new Settings{X=b.X,Y=b.Y,Width=b.Width,Height=b.Height,Font=UiSize,Kind=mode.SelectedIndex}.Save();}catch{MessageBox.Show(this,"設定を保存できません。EXE配置先の書込み権限を確認してください。","設定保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            password.ClearSecrets();editor.Clear();closing=true;Close();};
        SwitchMode();UpdateRun();
    }
    void SwitchMode(){bool html=mode.SelectedIndex==1;editor.Visible=!html;htmlEditor.Visible=html;if(html)htmlEditor.BringToFront();else editor.BringToFront();}
    void UpdateRun()=>generate.Enabled=!busy&&password.CanRun;
    void SetBusy(bool value){busy=value;editArea.Enabled=!value;password.Enabled=!value;allowCopy.Enabled=!value;checksum.Enabled=!value;output.Enabled=!value;outputPaste.Enabled=!value;generate.Enabled=!value&&password.CanRun;cancel.Enabled=value;}
    void DragFiles(object? sender,DragEventArgs e)=>e.Effect=!busy&&e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
    void PasteOutput(){try{if(Clipboard.ContainsFileDropList()){var p=Clipboard.GetFileDropList();if(p.Count!=1)throw new ArgumentException("出力先候補は1つだけ指定してください。");output.Text=Display.OutputCandidate(p[0]!);}else if(Clipboard.ContainsText())output.Text=Display.OutputCandidate(Clipboard.GetText());}catch(Exception ex){Error(this,ex);}}
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
        if(busy)return;byte[]? p=null;var watch=Stopwatch.StartNew();string stage="入力の検証";string? workFolder=null;HashSet<string> previousTemporary=[];
        try{
            SetBusy(true);p=password.Take(true);BodyKind kind=(BodyKind)mode.SelectedIndex;string body=kind==BodyKind.HTML?await htmlEditor.GetContent():editor.Text;Limits.BodyText(body);Limits.Inputs(paths);
            if(kind==BodyKind.HTML){body=SafeHtml.ValidateEditor(body,out bool warning);if(warning&&MessageBox.Show(this,"HTMLに不正な構造または危険な要素があります。安全に正規化した本文で暗号化しますか？","HTML安全性",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes){status.Text="暗号化をキャンセルしました。入力内容は保持しています。";return;}}
            string candidate=string.IsNullOrWhiteSpace(output.Text)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SecureMessage.exe"):Display.OutputCandidate(output.Text);
            using var d=new SaveFileDialog{Filter="実行ファイル (*.exe)|*.exe",FileName=Path.GetFileName(candidate),InitialDirectory=Path.GetDirectoryName(candidate),DefaultExt="exe",AddExtension=true,OverwritePrompt=true};
            if(d.ShowDialog(this)!=DialogResult.OK){status.Text="保存先の選択をキャンセルしました。入力内容は保持しています。";return;}
            string dest=Path.GetFullPath(d.FileName);if(!Path.GetExtension(dest).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("出力ファイルの拡張子は.exeにしてください。");
            if(string.Equals(dest,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("CipherCapsule本体には上書きできません。");
            if(checksum.Checked&&File.Exists(dest+".sha512")&&MessageBox.Show(this,"既存のチェックサムファイルを置き換えますか？\n"+dest+".sha512","上書き確認",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes){status.Text="暗号化をキャンセルしました。";return;}
            output.Text=dest;workFolder=Path.GetDirectoryName(dest)!;previousTemporary=Directory.EnumerateFiles(workFolder,".cc-*.tmp").ToHashSet(StringComparer.OrdinalIgnoreCase);cts=new();result.Clear();var progress=new Progress<CapsuleProgress>(x=>{stage=x.Stage;status.Text=x.Stage+(x.Total>1?$" {Display.Size(x.Done)} / {Display.Size(x.Total)}":"");bar.Value=(int)Math.Clamp(x.Done*1000/Math.Max(1,x.Total),0,1000);});
            string[] inputs=paths.ToArray();byte[] pw=p;bool sum=checksum.Checked,copy=allowCopy.Checked;stage="暗号化";
            var package=await Task.Run(()=>{using var template=Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewerTemplate")??throw new IOException();return PackageWriter.Generate(template,dest,body,kind,inputs,pw,sum,true,cts.Token,progress,copy);});
            watch.Stop();string message=$"暗号化が完了しました。\r\n\r\nファイル名：{Path.GetFileName(package.Path)}\r\n保存先：{Path.GetDirectoryName(package.Path)}\r\nフルパス：{package.Path}\r\nファイルサイズ：{Display.Size(package.Size)} ({package.Size:N0} B)\r\n処理時間：{Display.Time(watch.Elapsed)}\r\nSHA-512：{(package.Sha512!=null?"正常に生成しました。":"出力しない設定です。")}\r\nSHA-256：{package.Sha256}";
            result.Text=message;status.Text="暗号化完了。パスワードと編集内容はこのセッション内で保持しています。";ResultDialog(message,"暗号化成功");
        }
        catch(Exception ex){watch.Stop();string explanation=ex is OperationCanceledException?"処理をキャンセルしました。":ex is ArgumentException?ex.Message:ex is UnauthorizedAccessException?"アクセスが拒否されました。保存先の書込み権限とファイルの使用状況を確認してください。":ex is IOException?"ファイルを処理できません。空き容量・ファイルの使用状況・入力ファイルの変更を確認してください。"+ (ex.Message.StartsWith("出力の復元")?"\r\n"+ex.Message:""):"暗号化を完了できません。入力内容と実行環境を確認してください。";
            string cleanup="一時ファイルを作成する前に終了しました。";if(workFolder!=null)try{var remaining=Directory.EnumerateFiles(workFolder,".cc-*.tmp").Where(x=>!previousTemporary.Contains(x)).ToArray();cleanup=remaining.Length==0?"一時ファイルの後始末：残存なしを確認しました。":"一時・退避ファイルが残っています。保管して手動確認してください："+string.Join("、",remaining);}catch{cleanup="一時ファイルの後始末結果を確認できません。保存先を手動確認してください。";}
            string message=$"{(ex is OperationCanceledException?"キャンセル":"暗号化失敗")}\r\n処理：{stage}\r\n原因・対処：{explanation}\r\n経過時間：{Display.Time(watch.Elapsed)}\r\n{cleanup}\r\n入力済みパスワード・編集内容は保持しています。";result.Text=message;status.Text="処理を完了できませんでした。入力内容は保持しています。";ResultDialog(message,"暗号化結果");}
        finally{Sodium.Wipe(p);cts?.Dispose();cts=null;SetBusy(false);}
    }
    void ResultDialog(string message,string title){using var f=new AppForm{Text=title,Size=new Size(900,520)};var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12)};layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=message},0,0);layout.Controls.Add(Button("閉じる",(_,_)=>f.Close()),0,1);f.Controls.Add(layout);f.ShowDialog(this);}
    void ShowNotices(){var b=new System.Text.StringBuilder();var a=Assembly.GetExecutingAssembly();foreach(string name in a.GetManifestResourceNames().Where(n=>n.StartsWith("License.")).Order()){using var s=a.GetManifestResourceStream(name)!;using var r=new StreamReader(s);b.AppendLine(name[8..]).AppendLine(r.ReadToEnd()).AppendLine();}ResultDialog(b.ToString(),"CipherCapsule — ライセンス・注意事項");}
}
