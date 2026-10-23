using CipherCapsule.Core;
using CipherCapsule.UI;
using System.Diagnostics;
namespace CipherCapsule.Viewer;

// The destination comes only from the completed save, never from displayed text.
public static class SavedFolder
{
    public static string Parent(string savedPath)
    {
        ValidateAbsolute(savedPath);
        return Path.GetDirectoryName(Path.GetFullPath(savedPath))??throw new ArgumentException("保存先を確認できません。");
    }
    static void ValidateAbsolute(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.Any(char.IsControl)||path.StartsWith(@"\\?\")||path.StartsWith(@"\\.\"))
            throw new ArgumentException("保存先の絶対パスを確認できません。");
    }
    public static ProcessStartInfo StartInfo(string folder)
    {
        ValidateAbsolute(folder);folder=Path.GetFullPath(folder);
        if(!Directory.Exists(folder))throw new DirectoryNotFoundException();
        using(var entries=Directory.EnumerateFileSystemEntries(folder).GetEnumerator())_ = entries.MoveNext();
        var info=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),UseShellExecute=false};
        info.ArgumentList.Add(folder);return info;
    }
    public static string? TryOpen(string folder)=>TryOpen(folder,p=>{using var process=Process.Start(p)??throw new IOException();});
    public static string? TryOpen(string folder,Action<ProcessStartInfo> start)
    {
        try{start(StartInfo(folder));return null;}
        catch(DirectoryNotFoundException){return "保存先フォルダが存在しません。移動・削除されていないか確認してください。添付ファイルの保存結果は変わりません。";}
        catch(UnauthorizedAccessException){return "保存先フォルダにアクセスできません。アクセス権を確認してください。添付ファイルの保存結果は変わりません。";}
        catch{return "保存先フォルダを開けませんでした。場所・アクセス権・エクスプローラーの状態を確認してください。添付ファイルの保存結果は変わりません。";}
    }
}
public static class AttachmentResults
{
    public static ReadOnlyDialog Single(string message,string savedPath,Action<ProcessStartInfo>? launch=null,Action<string>? warn=null)
        =>Create("保存完了",message,SavedFolder.Parent(savedPath),launch,warn);
    public static ReadOnlyDialog Batch(BatchSaveResult result,Action<ProcessStartInfo>? launch=null,Action<string>? warn=null)
    {
        string message=$"保存成功：{result.Succeeded}件\r\n保存失敗：{result.Failed}件\r\n未処理：{result.Unprocessed}件\r\n保存先：{result.Folder}\r\n結果：{result.Reason}\r\n処理時間：{Display.Time(result.Elapsed)}";
        return Create(result.Failed==0&&!result.Canceled?"一括保存完了":"一括保存結果",message,result.Succeeded>0?result.Folder:null,launch,warn);
    }
    static ReadOnlyDialog Create(string title,string message,string? folder,Action<ProcessStartInfo>? launch,Action<string>? warn)
    {
        var f=new ReadOnlyDialog(title,message,new Size(900,520),true);
        if(folder==null)return f;
        var open=AppForm.Button("保存先フォルダを開く",(_,_)=>{
            string? warning=launch==null?SavedFolder.TryOpen(folder):SavedFolder.TryOpen(folder,launch);
            if(warning!=null){if(warn!=null)warn(warning);else MessageBox.Show(f,warning,"保存先フォルダ",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        });
        var group=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Anchor=AnchorStyles.None,Margin=new Padding(0)};
        var row=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0)};
        row.ColumnStyles.Add(new(SizeType.Percent,50));row.ColumnStyles.Add(new(SizeType.AutoSize));row.ColumnStyles.Add(new(SizeType.Percent,50));
        var layout=f.Controls.OfType<TableLayoutPanel>().Single();layout.Controls.Remove(f.CloseButton);
        group.Controls.Add(open);group.Controls.Add(f.CloseButton);row.Controls.Add(group,1,0);layout.Controls.Add(row,0,1);
        void SizeButtons(){int dpi=f.DeviceDpi;int height=(int)Math.Ceiling(40*Math.Max(1,f.UiSize/12f)*dpi/96f);open.MinimumSize=new Size(0,height);f.CloseButton.MinimumSize=new Size(0,height);open.Margin=new Padding(0,0,(int)Math.Ceiling(12*dpi/96f),0);f.CloseButton.Margin=new Padding(0);}
        SizeButtons();f.FontChanged+=(_,_)=>SizeButtons();f.DpiChanged+=(_,_)=>SizeButtons();
        f.Content.Select(0,0);f.ActiveControl=f.CloseButton;return f;
    }
}
