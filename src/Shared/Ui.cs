using CipherCapsule.Core;
using System.Runtime.InteropServices;

namespace CipherCapsule.UI;
public class AppForm : Form
{
    public float UiSize { get; private set; } = 12;
    public AppForm()
    {
        Font = new Font("Yu Gothic UI",12); AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.CenterScreen; MinimumSize=new Size(640,480); KeyPreview=true;
        try { Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }
    }
    public void SetSize(float size) { if(!float.IsFinite(size))return;float next=Math.Clamp(size,9,17);if(next==UiSize)return;UiSize=next;Font=new Font("Yu Gothic UI",UiSize);OnFontSizeChanged(); }
    protected virtual void OnFontSizeChanged() { }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(HandleZoomKey(keyData))return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
    public bool HandleZoomKey(Keys keyData)
    {
        if((keyData&Keys.Control)==0||(keyData&Keys.Alt)!=0)return false;
        var key=keyData&Keys.KeyCode;
        if(key is Keys.Add or Keys.Oemplus){SetSize(UiSize+1);return true;}
        if(key is Keys.Subtract or Keys.OemMinus){SetSize(UiSize-1);return true;}
        return false;
    }
    public static Button Button(string text,EventHandler? handler=null) { var b=new Button { Text=text,AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(4),MinimumSize=new Size(0,36) }; if(handler!=null)b.Click+=handler; return b; }
    public static FlowLayoutPanel Flow(params Control[] controls) { var f=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0) }; f.Controls.AddRange(controls); return f; }
    public static Label Label(string s)=>new() { Text=s,AutoSize=true,Margin=new Padding(4,8,4,4) };
    public static Task PostModal(Control owner,Action show)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.BeginInvoke(new Action(()=>{try{show();completion.TrySetResult();}catch(Exception e){completion.TrySetException(e);}}));return completion.Task;
    }
    public static void Error(IWin32Window owner,Exception ex)
    {
        string message=ex switch {
            BrowserFailure failure=>failure.UserMessage,
            OperationCanceledException=>"キャンセルしました。",
            System.Security.Cryptography.CryptographicException=>CipherCapsule.Core.Container.AuthFailure,
            UnauthorizedAccessException=>"アクセスが拒否されました。書込み権限・ファイルの使用状況を確認してください。",
            IOException=>"ファイルを処理できません。空き容量・入力の変更・保存先・使用状況を確認してください。",
            ArgumentException or InvalidDataException=>ex.Message,
            _=>"処理に失敗しました。入力内容・実行環境を確認してください。" };
        MessageBox.Show(owner,message,"CipherCapsule",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
}
public sealed class PasswordPanel : TableLayoutPanel
{
    public readonly TextBox Primary=new() { UseSystemPasswordChar=true,Dock=DockStyle.Fill,MaxLength=512,AccessibleName="パスワード" };
    public readonly TextBox Confirm=new() { UseSystemPasswordChar=true,Dock=DockStyle.Fill,MaxLength=512,AccessibleName="パスワード確認" };
    readonly Label confirmLabel=AppForm.Label("パスワード確認");
    public readonly Button Eye;
    public bool Revealed { get; private set; }
    public bool CanRun => Primary.Text.Length>0 && (Revealed || Primary.Text==Confirm.Text);
    public event EventHandler? ValidityChanged;
    public PasswordPanel()
    {
        AutoSize=true; Dock=DockStyle.Fill; ColumnCount=3; RowCount=2;
        ColumnStyles.Add(new(SizeType.AutoSize)); ColumnStyles.Add(new(SizeType.Percent,100)); ColumnStyles.Add(new(SizeType.AutoSize));
        Eye=AppForm.Button("表示",(_,_)=>Toggle()); Eye.AccessibleName="パスワードを表示／非表示";
        Eye.Image=EyeImage(false); Eye.TextImageRelation=TextImageRelation.ImageBeforeText;
        Controls.Add(AppForm.Label("パスワード"),0,0); Controls.Add(Primary,1,0); Controls.Add(Eye,2,0);
        Controls.Add(confirmLabel,0,1); Controls.Add(Confirm,1,1);
        Primary.TextChanged+=(_,_)=>ValidityChanged?.Invoke(this,EventArgs.Empty); Confirm.TextChanged+=(_,_)=>ValidityChanged?.Invoke(this,EventArgs.Empty);
    }
    public void Toggle()=>SetRevealed(!Revealed);
    public void SetRevealed(bool revealed)
    {
        if(Revealed==revealed)return;Revealed=revealed; Primary.UseSystemPasswordChar=!Revealed; Confirm.Clear(); Confirm.Visible=!Revealed; confirmLabel.Visible=!Revealed;
        Eye.Text=Revealed?"隠す":"表示"; var old=Eye.Image;Eye.Image=EyeImage(Revealed);old?.Dispose(); ValidityChanged?.Invoke(this,EventArgs.Empty);
    }
    public byte[] Take(bool creation)
    {
        if(!CanRun)throw new ArgumentException("パスワードが空欄、または確認入力が一致していません。");
        Limits.Password(Primary.Text,creation); return Limits.Utf8.GetBytes(Primary.Text);
    }
    public void ClearSecrets() { Primary.Clear(); Confirm.Clear(); }
    static Bitmap EyeImage(bool closed)
    {
        var b=new Bitmap(30,22);using var g=Graphics.FromImage(b);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var p=new Pen(Color.FromArgb(25,65,85),2);
        if(closed){g.DrawArc(p,2,-4,26,21,20,140);g.DrawLine(p,5,14,2,19);g.DrawLine(p,15,17,15,22);g.DrawLine(p,25,14,28,19);}
        else{using var shape=new System.Drawing.Drawing2D.GraphicsPath();shape.AddBezier(2,11,8,1,22,1,28,11);shape.AddBezier(28,11,22,21,8,21,2,11);g.DrawPath(p,shape);g.FillEllipse(Brushes.Teal,11,7,8,8);}
        return b;
    }
    protected override void Dispose(bool disposing){if(disposing)Eye.Image?.Dispose();base.Dispose(disposing);}
}
public sealed class PasswordDialog : AppForm
{
    public readonly PasswordPanel Password=new();
    readonly Button unlock,cancel; readonly Label status=Label("パスワードを入力してください。");
    CancellationTokenSource? cts; bool working;
    public Container.UnlockedCapsule? Capsule { get; private set; }
    public PasswordDialog(string path)
    {
        Text="CipherCapsule — パスワード認証"; ClientSize=new Size(720,300); MinimumSize=new Size(640,330);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(14),RowCount=4,ColumnCount=1 };
        unlock=Button("復号して開く",async(_,_)=>await Open(path)); unlock.Enabled=false;
        cancel=Button("閉じる／キャンセル",(_,_)=>{ if(working)cts?.Cancel(); else Close(); });
        layout.Controls.Add(Label("マスク時は確認入力も必要です。表示切替で1行にできます。")); layout.Controls.Add(Password); layout.Controls.Add(status); layout.Controls.Add(Flow(unlock,cancel)); Controls.Add(layout);
        Password.ValidityChanged+=(_,_)=>unlock.Enabled=!working&&Password.CanRun; AcceptButton=unlock; CancelButton=cancel;
        FormClosing+=(_,e)=>{ if(working) { e.Cancel=true; cts?.Cancel(); } else Password.ClearSecrets(); };
    }
    async Task Open(string path)
    {
        byte[]? p=null;
        try {
            p=Password.Take(false); working=true; unlock.Enabled=false; Password.Enabled=false; cts=new();
            status.Text="パスワードと全添付の暗号文を検証しています…";
            byte[] key=p; Capsule=await Task.Run(()=>CipherCapsule.Core.Container.Unlock(path,key,cts.Token));
            Password.ClearSecrets(); DialogResult=DialogResult.OK;
        }
        catch(Exception ex) { Error(this,ex); status.Text="再入力してください。"; }
        finally { Sodium.Wipe(p); working=false; Password.Enabled=true; unlock.Enabled=Password.CanRun; cts?.Dispose(); cts=null; if(Capsule!=null)Close(); }
    }
}
