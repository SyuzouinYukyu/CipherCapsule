using CipherCapsule.Core;
using CipherCapsule.UI;
namespace CipherCapsule.Viewer;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppForm.Error(Form.ActiveForm ?? new Form(),e.Exception);
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
