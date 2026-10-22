using CipherCapsule.UI;
using CipherCapsule.Viewer;
using System.Diagnostics;

internal static partial class Program
{
    static string ExitLog=>Path.GetFullPath(Path.Combine(Root,"../Work/exit-diagnostic.log"));
    static void ExitNote(string note)=>File.AppendAllText(ExitLog,DateTimeOffset.Now.ToString("O")+" "+note+Environment.NewLine);
    sealed class CloseTrace:NativeWindow
    {
        readonly string label;
        public CloseTrace(Form form,string label){this.label=label;form.HandleCreated+=(_,_)=>AssignHandle(form.Handle);if(form.IsHandleCreated)AssignHandle(form.Handle);form.FormClosing+=(_,e)=>ExitNote(label+" FormClosing "+e.CloseReason+" cancel="+e.Cancel+"\n"+Environment.StackTrace);form.FormClosed+=(_,e)=>ExitNote(label+" FormClosed "+e.CloseReason);}
        protected override void WndProc(ref Message m){if(m.Msg is 0x10 or 0x11 or 0x16 or 0x112)ExitNote(label+" MSG="+m.Msg.ToString("X")+" WP="+m.WParam.ToInt64().ToString("X"));base.WndProc(ref m);}
    }
    static int TraceUnexpectedExit()
    {
        ExitNote("START pid="+Environment.ProcessId);
        Application.ThreadException+=(_,e)=>ExitNote("THREAD_EXCEPTION "+e.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>ExitNote("UNHANDLED "+e.ExceptionObject);
        Application.ApplicationExit+=(_,_)=>ExitNote("APPLICATION_EXIT");
        Application.ThreadExit+=(_,_)=>ExitNote("THREAD_EXIT");
        using var clock=new System.Windows.Forms.Timer{Interval=5000};clock.Tick+=(_,_)=>ExitNote("HEARTBEAT forms="+string.Join("|",Application.OpenForms.Cast<Form>().Select(f=>f.Text)));clock.Start();
        using var password=new PasswordDialog(Path.GetFullPath(Path.Combine(Root,"../Work/integration/TEXT-message.exe")));
        var authTrace=new CloseTrace(password,"AUTH");
        password.Shown+=(_,_)=>{password.Password.Primary.Text="Gui112-Test!2026";password.Password.Confirm.Text="Gui112-Test!2026";((Button)password.AcceptButton!).PerformClick();};
        var result=password.ShowDialog();ExitNote("AUTH_RESULT "+result+" capsule="+(password.Capsule!=null));
        if(result!=DialogResult.OK||password.Capsule==null)return 1;
        using var capsule=password.Capsule;using var viewer=new ViewerForm(capsule);var viewerTrace=new CloseTrace(viewer,"VIEWER");viewer.Shown+=(_,_)=>ExitNote("VIEWER_SHOWN");
        Application.Run(viewer);ExitNote("RUN_RETURNED");GC.KeepAlive(authTrace);GC.KeepAlive(viewerTrace);return 0;
    }
}
