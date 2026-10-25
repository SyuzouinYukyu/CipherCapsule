using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CipherCapsule.UI;

namespace CipherCapsule.Builder;

public sealed record CcapIconValue(bool KeyExists,bool HasValue,string? Value,RegistryValueKind Kind);
public interface ICcapIconStore
{
    CcapIconValue Read();
    void Restore(CcapIconValue value);
    bool IsIconReferenced(string iconPath);
}

// Migration only: this class has no install/register API.
public sealed class CurrentUserCcapIconStore:ICcapIconStore
{
    const string KeyPath=@"Software\Classes\.ccap\DefaultIcon";
    public CcapIconValue Read()
    {
        using var key=Registry.CurrentUser.OpenSubKey(KeyPath);
        if(key==null)return new(false,false,null,RegistryValueKind.String);
        if(!key.GetValueNames().Contains(""))return new(true,false,null,RegistryValueKind.String);
        var kind=key.GetValueKind("");
        if(kind is not(RegistryValueKind.String or RegistryValueKind.ExpandString))throw new IOException("既存アイコン値の種類を確認できません。現在の設定を維持します。");
        return new(true,true,(string)key.GetValue("","",RegistryValueOptions.DoNotExpandEnvironmentNames)!,kind);
    }
    public void Restore(CcapIconValue value)
    {
        if(value.HasValue)
        {
            using var key=Registry.CurrentUser.CreateSubKey(KeyPath,true);
            key.SetValue("",value.Value!,value.Kind);
        }
        else
        {
            using var key=Registry.CurrentUser.OpenSubKey(KeyPath,true);
            key?.DeleteValue("",false);
            // Retain an empty key: deleting the key could race a new named value.
            // An absent default value restores the original effective icon setting.
        }
    }
    public bool IsIconReferenced(string iconPath)
    {
        if(Read().Value?.Contains(iconPath,StringComparison.OrdinalIgnoreCase)==true)return true;
        // Read-only check of registered class icons, including other associations.
        // Access/format uncertainty propagates; the cached ICO is then retained.
        using var classes=Registry.ClassesRoot;
        foreach(string name in classes.GetSubKeyNames())
        {
            using var icon=classes.OpenSubKey(name+@"\DefaultIcon");
            object? value=icon?.GetValue("",null,RegistryValueOptions.DoNotExpandEnvironmentNames);
            if(value is string text&&Environment.ExpandEnvironmentVariables(text).Replace('/','\\').Contains(iconPath,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }
}

public sealed class CcapIconRegistration
{
    sealed record State(int Version,CcapIconValue Original,string InstalledValue);
    // Fingerprint of the ICO shipped by the retired registration feature.
    public const string LegacyDigest="d5bfc6077c4fc9a0e483f26dd6eba24fb3084f24a047c2b5b72a4cb3cb110dc0";
    readonly ICcapIconStore store;readonly string folder,statePath;readonly Action notify;
    public string IconPath{get;}
    string IconValue=>$"\"{IconPath}\",0";
    public CcapIconRegistration(ICcapIconStore store,string folder,Action notify)
    {
        this.store=store;this.folder=ManagedFiles.CheckPath(folder);this.notify=notify;
        IconPath=Path.Combine(this.folder,"ccap-"+LegacyDigest+".ico");
        statePath=Path.Combine(this.folder,"ccap-icon-registration.json");
    }
    public static CcapIconRegistration ForCurrentUser()=>new(new CurrentUserCcapIconStore(),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CipherCapsule","Icons"),NotifyShell);
    public bool Restore()
    {
        ManagedFiles.CheckPath(statePath);ManagedFiles.CheckPath(IconPath);
        // Missing state never causes a registry read/write or a directory creation.
        if(!File.Exists(statePath))
        {
            if(File.Exists(IconPath))throw new IOException("アイコンの復元情報がないため、旧ICOと現在の設定を保持します。\n"+IconPath);
            return false;
        }
        byte[] snapshot=ReadBounded(statePath,32768);State state=LoadState(snapshot);
        if(File.Exists(IconPath))ValidateIcon();
        var current=store.Read();bool changed=false;
        if(IsInstalled(current,state))
        {
            // Recheck immediately before the single value restoration.
            if(store.Read()!=current)throw Conflict();
            store.Restore(state.Original);changed=true;
        }
        else if(!MatchesOriginal(current,state.Original))throw Conflict();
        if(!MatchesOriginal(store.Read(),state.Original))throw new IOException("元のアイコン設定への復元を確認できません。ICO・復元情報は保持しています。");
        if(changed)notify();
        if(File.Exists(IconPath))
        {
            if(store.IsIconReferenced(IconPath))throw new IOException("旧ICOを参照する設定が残っているため、ICO・復元情報は保持します。\n"+IconPath);
            // Check the actual bytes and ancestry again before deleting this one file.
            ValidateIcon();ManagedFiles.CheckPath(IconPath);
            if(!MatchesOriginal(store.Read(),state.Original))throw Conflict();
            File.Delete(IconPath);
        }
        ManagedFiles.CheckPath(statePath);
        if(!ReadBounded(statePath,32768).AsSpan().SequenceEqual(snapshot))throw new IOException("復元情報が変更されたため整理を中止しました。");
        if(!MatchesOriginal(store.Read(),state.Original))throw Conflict();
        File.Delete(statePath);ManagedFiles.RemoveEmpty(folder);return true;
    }
    static byte[] ReadBounded(string path,int max)
    {
        ManagedFiles.CheckPath(path);using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length is <1||file.Length>max)throw new InvalidDataException("旧アイコンの復元情報が不正です。設定を維持します。");
        byte[] b=new byte[(int)file.Length];file.ReadExactly(b);return b;
    }
    State LoadState(byte[] data)
    {
        using var document=JsonDocument.Parse(data);var root=document.RootElement;
        static void Fields(JsonElement obj,params string[] names)
        {
            if(obj.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("旧アイコン復元情報の構造が不正です。");
            var fields=obj.EnumerateObject().Select(p=>p.Name).ToArray();
            if(fields.Length!=names.Length||fields.Distinct().Count()!=fields.Length||names.Any(n=>!fields.Contains(n)))throw new InvalidDataException("旧アイコン復元情報の項目が不正です。");
        }
        Fields(root,"Version","Original","InstalledValue");Fields(root.GetProperty("Original"),"KeyExists","HasValue","Value","Kind");
        var state=JsonSerializer.Deserialize<State>(data);
        if(state==null||state.Version!=1||state.InstalledValue!=IconValue||state.Original==null||
            state.Original.HasValue&&(!state.Original.KeyExists||state.Original.Value==null||state.Original.Kind is not(RegistryValueKind.String or RegistryValueKind.ExpandString))||
            !state.Original.HasValue&&state.Original.Value!=null||state.Original.Value?.Contains(IconPath,StringComparison.OrdinalIgnoreCase)==true)
            throw new InvalidDataException("旧アイコンの復元情報が不整合です。設定を維持します。");
        return state;
    }
    void ValidateIcon()
    {
        byte[] bytes=ReadBounded(IconPath,32029);
        if(bytes.Length!=32029||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(LegacyDigest,StringComparison.OrdinalIgnoreCase))throw new IOException("旧ICOの内容を確認できません。ファイルと設定を保持します。");
    }
    static bool IsInstalled(CcapIconValue c,State s)=>c.KeyExists&&c.HasValue&&c.Kind==RegistryValueKind.String&&c.Value==s.InstalledValue;
    static bool MatchesOriginal(CcapIconValue c,CcapIconValue original)=>c.HasValue==original.HasValue&&(!original.HasValue||c.Value==original.Value&&c.Kind==original.Kind);
    static IOException Conflict()=>new("別の操作でCCAPアイコン設定が変更されています。現在の設定と復元情報を保持します。");
    static void NotifyShell()=>SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
    [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
}
