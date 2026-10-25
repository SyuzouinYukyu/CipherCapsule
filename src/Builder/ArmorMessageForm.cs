using CipherCapsule.Core;
using CipherCapsule.UI;

namespace CipherCapsule.Builder;

public sealed class ArmorMessageForm:AppForm
{
    readonly ComboBox mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="暗号メッセージの操作",AutoSize=true};
    readonly TextBox plain=new(){Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,MaxLength=AsciiArmor.MaxTextBytes+1,AccessibleName="暗号化するプレーンテキスト"};
    readonly RichTextBox cipher=new(){Multiline=true,ScrollBars=RichTextBoxScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,MaxLength=AsciiArmor.MaxArmorLength+1,AccessibleName="ASCII Armor暗号文",DetectUrls=false};
    readonly RichTextBox decoded=new(){Multiline=true,ReadOnly=true,ScrollBars=RichTextBoxScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AccessibleName="認証済みの復号本文",DetectUrls=false};
    readonly PasswordPanel password=new();
    readonly ArmorMessageState state=new();
    readonly Label status=Label("本文はUTF-8で1～16KiB。添付・HTMLは非対応です。");
    readonly Button encrypt,decrypt,copy,paste,cancel,close;
    CancellationTokenSource? cts;bool busy,closePending;
    readonly string temporaryRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CipherCapsule","ArmorMessages");

    public ArmorMessageForm(float size)
    {
        Text="CipherCapsule v1.1.9 — 暗号メッセージ";ClientSize=new Size(1080,880);MinimumSize=new Size(800,760);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=12,Padding=new Padding(12)};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<12;i++)layout.RowStyles.Add(new(i==3?SizeType.Percent:i==5?SizeType.Percent:i==7?SizeType.Percent:SizeType.AutoSize,i==3?25:i==5?45:i==7?30:0));
        mode.Items.AddRange(["暗号化","復号"]);mode.SelectedIndex=0;
        layout.Controls.Add(Label("CipherCapsule独自のASCII Armor形式（PGP互換ではありません）。"),0,0);
        layout.Controls.Add(Flow(Label("操作"),mode),0,1);
        layout.Controls.Add(Label("暗号化する本文（貼り付け操作では上書きしません）"),0,2);layout.Controls.Add(plain,0,3);
        layout.Controls.Add(Label("暗号文（コピーで生成全文を送信／貼り付けで受信）"),0,4);layout.Controls.Add(cipher,0,5);
        layout.Controls.Add(Label("復号した本文（認証成功後だけ表示）"),0,6);layout.Controls.Add(decoded,0,7);
        layout.Controls.Add(password,0,8);
        encrypt=Button("暗号化する",async(_,_)=>await Run(true));decrypt=Button("復号する",async(_,_)=>await Run(false));
        copy=Button("コピー",(_,_)=>Copy());copy.AccessibleName="生成した暗号文全体をコピー";
        paste=Button("貼り付け",(_,_)=>Paste());paste.AccessibleName="暗号文をクリップボードから貼り付け";
        cancel=Button("キャンセル",(_,_)=>cts?.Cancel());close=Button("閉じる",(_,_)=>Close());
        layout.Controls.Add(status,0,9);layout.Controls.Add(Flow(encrypt,decrypt,copy,paste,cancel,close),0,10);
        layout.Controls.Add(Label("暗号文はOSのクリップボード履歴・同期の対象になる場合があります。"),0,11);
        Controls.Add(layout);SetSize(size);
        mode.SelectedIndexChanged+=(_,_)=>UpdateButtons();password.ValidityChanged+=(_,_)=>UpdateButtons();
        cipher.TextChanged+=(_,_)=>{state.CipherChanged();decoded.Clear();UpdateButtons();};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;closePending=true;cts?.Cancel();return;}state.BeginOperation();plain.Clear();cipher.Clear();decoded.Clear();password.ClearSecrets();};
        UpdateButtons();
    }
    // Wrap all explanatory/status labels to the actual client width, including
    // 17pt and resized windows. Text areas keep their percentage rows.
    void WrapLabels()
    {
        foreach(var panel in Controls.OfType<TableLayoutPanel>())
            foreach(var label in panel.Controls.OfType<Label>())label.MaximumSize=new Size(Math.Max(120,panel.ClientSize.Width-panel.Padding.Horizontal-label.Margin.Horizontal),0);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);WrapLabels();}
    protected override void OnFontSizeChanged(){base.OnFontSizeChanged();WrapLabels();}
    void UpdateButtons()
    {
        encrypt.Enabled=!busy&&mode.SelectedIndex==0&&password.CanRun;
        decrypt.Enabled=!busy&&mode.SelectedIndex==1&&password.CanRun;
        copy.Enabled=!busy&&state.CanCopy(cipher.Text);paste.Enabled=!busy;cancel.Enabled=busy;
        plain.Enabled=!busy;cipher.ReadOnly=busy;password.Enabled=!busy;mode.Enabled=!busy;
    }
    void Copy()
    {
        try{Clipboard.SetText(state.CopyGenerated(cipher.Text),TextDataFormat.UnicodeText);status.Text="生成した暗号文全体をコピーしました。";}
        catch{MessageBox.Show(this,"暗号文をコピーできませんでした。クリップボードの使用状況を確認してください。","コピー",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    void Paste()
    {
        state.CipherChanged();decoded.Clear();UpdateButtons();
        try
        {
            if(!Clipboard.ContainsText(TextDataFormat.UnicodeText))throw new InvalidDataException("クリップボードにテキストの暗号メッセージがありません。");
            string value=Clipboard.GetText(TextDataFormat.UnicodeText);ArmorMessageState.ValidatePaste(value);
            cipher.Text=value;mode.SelectedIndex=1;status.Text="暗号文を取り込みました。パスワードを入力して復号してください。";
        }
        catch(Exception e){status.Text="貼り付けできませんでした。";if(e is InvalidDataException or ArgumentException)Error(this,e);else MessageBox.Show(this,"クリップボードを読み取れませんでした。","貼り付け",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    async Task Run(bool creation)
    {
        if(busy)return;byte[]? secret=null;state.BeginOperation();decoded.Clear();UpdateButtons();
        try
        {
            if(creation)AsciiArmor.ValidateText(plain.Text);else _=AsciiArmor.Decode(cipher.Text);
            secret=password.Take(creation);string input=creation?plain.Text:cipher.Text;
            busy=true;cts=new();UpdateButtons();status.Text="認証付き暗号処理中です（Argon2id・256MiB）…";
            byte[] key=secret;var ct=cts.Token;
            string value=await Task.Run(()=>creation?ArmorMessages.Encrypt(input,key,temporaryRoot,ct):ArmorMessages.Decrypt(input,key,temporaryRoot,ct));
            if(closePending)return;
            if(creation){cipher.Text=value;state.EncryptionSucceeded(value);status.Text="暗号化しました。「コピー」で送信できます。";}
            else{state.DecryptionSucceeded(value);decoded.Text=state.PlainResult;status.Text="認証に成功しました。";}
        }
        catch(Exception e){decoded.Clear();status.Text=e is OperationCanceledException?"キャンセルしました。":"処理に失敗しました。以前の復号結果は消去しました。";if(!closePending)Error(this,e);}
        finally{Sodium.Wipe(secret);password.ClearSecrets();cts?.Dispose();cts=null;busy=false;UpdateButtons();if(closePending)Close();}
    }
}
