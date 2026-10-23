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
            string? warning=AutomaticCcapIcon.Apply(()=>CcapIconRegistration.ForCurrentUser().Install(force:true));
            if(warning!=null)MessageBox.Show(builder,warning,"CCAPアイコン",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            if(args.Length>0)await builder.OpenCcap(args);
        };
        Application.Run(builder);
    }
}
