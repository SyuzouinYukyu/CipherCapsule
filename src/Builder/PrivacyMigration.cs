using System.Text.Json;
using CipherCapsule.UI;
namespace CipherCapsule.Builder;

public static class PrivacyMigration
{
    public static string? Run()
    {
        var warnings=new List<string>();
        void Attempt(Action action){try{action();}catch(Exception e){warnings.Add(e.Message);}}
        Attempt(()=>CcapIconRegistration.ForCurrentUser().Restore());
        string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CipherCapsule");
        Attempt(()=>RemoveLegacyDiagnostic(local));
        Attempt(()=>ManagedFiles.RemoveEmpty(Path.Combine(local,"ArmorMessages")));
        // Previous sessions carry no trustworthy ownership/exit lease. Retain them.
        Attempt(()=>{
            string legacy=ManagedFiles.CheckPath(Path.Combine(Path.GetTempPath(),"CipherCapsule-v118"));
            if(Directory.Exists(legacy)){
                if(Directory.EnumerateFileSystemEntries(legacy).Any())warnings.Add("過去のWebView2一時領域は所有元・未使用状態を確認できないため保持しています。\n"+legacy);
                else ManagedFiles.RemoveEmpty(legacy);
            }
        });
        return warnings.Count==0?null:string.Join("\n\n",warnings)+"\n\n暗号化・復号は引き続き利用できます。";
    }
    public static void RemoveLegacyDiagnostic(string local)
    {
        string folder=Path.Combine(local,"Diagnostics"),path=ManagedFiles.CheckPath(Path.Combine(folder,"editor-switch118.json"));
        if(!File.Exists(path)){ManagedFiles.RemoveEmpty(folder);return;}
        byte[] snapshot;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(file.Length>65536)throw new IOException("旧診断ファイルを特定できないため保持します。\n"+path);
            snapshot=new byte[(int)file.Length];file.ReadExactly(snapshot);
            using var json=JsonDocument.Parse(snapshot);var r=json.RootElement;
            string[] expected=["version","utc","ready","selected","errorType","hresult","events"];
            if(r.ValueKind!=JsonValueKind.Object||r.EnumerateObject().Count()!=expected.Length||expected.Any(n=>!r.TryGetProperty(n,out _))||
               r.GetProperty("version").GetString() is not("1.1.8" or "1.1.9")||r.GetProperty("events").ValueKind!=JsonValueKind.Array||
               r.GetProperty("events").GetArrayLength()>24||r.GetProperty("utc").ValueKind!=JsonValueKind.String||
               r.GetProperty("ready").ValueKind is not(JsonValueKind.True or JsonValueKind.False)||
               !r.GetProperty("selected").TryGetInt32(out _)||r.GetProperty("errorType").ValueKind!=JsonValueKind.String||!r.GetProperty("hresult").TryGetInt32(out _))
                throw new IOException("旧診断ファイルを特定できないため保持します。\n"+path);
        }
        ManagedFiles.CheckPath(path);
        if(!File.ReadAllBytes(path).AsSpan().SequenceEqual(snapshot))throw new IOException("旧診断ファイルが変更されたため保持します。\n"+path);
        File.Delete(path);ManagedFiles.RemoveEmpty(folder);
    }
}
