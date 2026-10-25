using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

string desktop="CC1110-"+Guid.NewGuid().ToString("N");
string exe=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
IntPtr handle=Native.CreateDesktop(desktop,null,IntPtr.Zero,0,0x01ff,IntPtr.Zero);
if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
try{
    var startup=new Native.STARTUPINFO{cb=Marshal.SizeOf<Native.STARTUPINFO>(),lpDesktop=@"WinSta0\"+desktop};
    var command=new StringBuilder("\""+exe+"\" \""+output+"\"");
    if(!Native.CreateProcess(exe,command,IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(exe),ref startup,out var info))throw new System.ComponentModel.Win32Exception();
    try{
        if(Native.WaitForSingleObject(info.hProcess,110000)!=0){using var process=Process.GetProcessById((int)info.dwProcessId);process.Kill(entireProcessTree:true);throw new TimeoutException("Only the owned isolated probe was stopped; inspect retained temp data");}
        if(!Native.GetExitCodeProcess(info.hProcess,out uint code))throw new System.ComponentModel.Win32Exception();
        Console.WriteLine(JsonSerializer.Serialize(new{desktop,pid=info.dwProcessId,exit_code=code,result=Path.Combine(output,"results.json"),desktop_switched=false}));
        return (int)code;
    }finally{Native.CloseHandle(info.hThread);Native.CloseHandle(info.hProcess);}
}finally{Native.CloseDesktop(handle);}

static class Native
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct STARTUPINFO{public int cb;public string? lpReserved,lpDesktop,lpTitle;public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;public short wShowWindow,cbReserved2;public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;}
    [StructLayout(LayoutKind.Sequential)]public struct PROCESS_INFORMATION{public IntPtr hProcess,hThread;public uint dwProcessId,dwThreadId;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr CreateDesktop(string name,string? device,IntPtr devmode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll")]public static extern bool CloseDesktop(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern bool CreateProcess(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string? currentDirectory,ref STARTUPINFO startup,out PROCESS_INFORMATION info);
    [DllImport("kernel32.dll")]public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]public static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
}
