using CipherCapsule.UI;
namespace CipherCapsule.Builder;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppForm.Error(Form.ActiveForm ?? new Form(),e.Exception);
        using var builder=new BuilderForm();
        builder.Shown+=async(_,_)=>{
            string? warning=PrivacyMigration.Run();
            if(warning!=null)MessageBox.Show(builder,warning,"旧設定の整理",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            if(args.Length>0)await builder.OpenCcap(args);
        };
        Application.Run(builder);
    }
}
