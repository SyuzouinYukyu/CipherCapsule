using CipherCapsule.UI;
namespace CipherCapsule.Builder;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppForm.Error(Form.ActiveForm ?? new Form(),e.Exception);
        Application.Run(new BuilderForm());
    }
}
