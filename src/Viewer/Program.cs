using CipherCapsule.Core;
using CipherCapsule.UI;
namespace CipherCapsule.Viewer;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppForm.Error(Form.ActiveForm ?? new Form(),e.Exception);
        var recovery=CipherCapsule.Core.SecureAttachment.Recover();
        if(recovery.Count>0){using var warning=new ReadOnlyDialog("平文一時領域の回復警告",string.Join("\r\n\r\n",recovery),new Size(900,600),true);warning.ShowDialog();}
        try {
            string path=Environment.ProcessPath!;
            using(var s=File.OpenRead(path))Container.Locate(s);
            using var password=new PasswordDialog(path);
            if(password.ShowDialog()!=DialogResult.OK || password.Capsule==null)return;
            using var capsule=password.Capsule; Application.Run(new ViewerForm(capsule));
        }
        catch(Exception ex) { AppForm.Error(new Form(),ex); }
    }
}
