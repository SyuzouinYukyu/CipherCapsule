using CipherCapsule.UI;
namespace CipherCapsule.Builder;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppForm.Error(Form.ActiveForm ?? new Form(),e.Exception);
        var recovery=CipherCapsule.Core.SecureAttachment.Recover();
        if(recovery.Count>0){using var warning=new ReadOnlyDialog("平文一時領域の回復警告",string.Join("\r\n\r\n",recovery),new Size(900,600),true);warning.ShowDialog();}
        using var builder=new BuilderForm();
        builder.Shown+=async(_,_)=>{
            string? warning=PrivacyMigration.Run();
            if(warning!=null)MessageBox.Show(builder,warning,"旧設定の整理",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            if(args.Length>0)await builder.OpenCcap(args);
        };
        Application.Run(builder);
    }
}
