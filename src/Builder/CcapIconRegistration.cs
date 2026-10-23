using Microsoft.Win32;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace CipherCapsule.Builder;

public sealed record CcapIconValue(bool KeyExists, bool HasValue, string? Value, RegistryValueKind Kind);
public interface ICcapIconStore
{
    CcapIconValue Read();
    void Set(string value);
    void Restore(CcapIconValue value);
}

// The extension's icon only. No UserChoice, open command, default app or HKLM writes.
public sealed class CurrentUserCcapIconStore : ICcapIconStore
{
    const string KeyPath = @"Software\Classes\.ccap\DefaultIcon";
    public CcapIconValue Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key == null) return new(false, false, null, RegistryValueKind.String);
        if (!key.GetValueNames().Contains("")) return new(true, false, null, RegistryValueKind.String);
        var kind = key.GetValueKind("");
        if (kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
            throw new InvalidDataException("既存のアイコン設定を安全に読み取れません。設定は変更しません。");
        return new(true, true, (string)key.GetValue("", "", RegistryValueOptions.DoNotExpandEnvironmentNames)!, kind);
    }
    public void Set(string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        key.SetValue("", value, RegistryValueKind.String);
    }
    public void Restore(CcapIconValue value)
    {
        if (value.HasValue)
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            key.SetValue("", value.Value!, value.Kind);
        }
        else
        {
            bool removeEmpty = false;
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
            {
                key?.DeleteValue("", false);
                removeEmpty = !value.KeyExists && key != null && key.ValueCount == 0 && key.SubKeyCount == 0;
            }
            if (removeEmpty) Registry.CurrentUser.DeleteSubKey(KeyPath, false);
        }
    }
}

public sealed class CcapIconRegistration
{
    sealed record State(int Version, CcapIconValue Original, string InstalledValue);
    readonly ICcapIconStore store;
    readonly string folder, statePath;
    readonly byte[] icon;
    readonly Action notify;
    public string IconPath { get; }
    public string IconValue => $"\"{IconPath}\",0";

    public CcapIconRegistration(ICcapIconStore store, string folder, byte[] icon, Action notify)
    {
        if (!Path.IsPathFullyQualified(folder) || folder.Any(char.IsControl)) throw new ArgumentException("アイコン保存先が不正です。");
        ValidateIcon(icon);
        this.store = store; this.folder = Path.GetFullPath(folder); this.icon = icon.ToArray(); this.notify = notify;
        string digest = Convert.ToHexString(SHA256.HashData(icon)).ToLowerInvariant();
        IconPath = Path.Combine(this.folder, $"ccap-{digest}.ico");
        statePath = Path.Combine(this.folder, "ccap-icon-registration.json");
    }

    public static CcapIconRegistration ForCurrentUser()
    {
        using var stream = typeof(CcapIconRegistration).Assembly.GetManifestResourceStream("CcapIcon")
            ?? throw new IOException("内蔵アイコンを読み込めません。");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!Path.IsPathFullyQualified(local)) throw new IOException("ユーザーの保存先を確認できません。");
        return new(new CurrentUserCcapIconStore(), Path.Combine(local, "CipherCapsule", "Icons"), buffer.ToArray(), NotifyShell);
    }

    public void Install()=>Install(false);
    public void Install(bool force)
    {
        EnsureUnlinkedFolder();
        State? state = LoadState();
        var current = store.Read();
        if (!force && state != null && current != state.Original && !IsInstalled(current, state)) throw Conflict();
        bool createdIcon = false;
        if (File.Exists(IconPath))
        {
            if (!File.ReadAllBytes(IconPath).AsSpan().SequenceEqual(icon))
                throw new IOException("アイコン保存先に別のファイルがあります。上書きしません。");
        }
        else { using var output = new FileStream(IconPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); output.Write(icon); createdIcon = true; }
        if (state == null)
        {
            state = new(1, current, IconValue);
            // Persist recovery information before attempting the registry write.
            using var output = new FileStream(statePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(output, state); output.Flush(true);
        }
        // Reassert the requested icon, retaining the original recovery snapshot.
        // An unchanged startup needs no write or Explorer cache notification.
        if (IsInstalled(current, state)) { if (createdIcon) notify(); return; }
        store.Set(IconValue);
        if (!IsInstalled(store.Read(), state)) throw new IOException("アイコン設定の反映を確認できません。復元情報は保持しています。");
        notify();
    }

    public bool Restore()
    {
        EnsureUnlinkedFolder();
        State? state = LoadState();
        if (state == null) return false;
        var current = store.Read();
        if (IsInstalled(current, state)) store.Restore(state.Original);
        else if (current != state.Original) throw Conflict();
        if (store.Read() != state.Original) throw new IOException("元のアイコン設定への復元を確認できません。復元情報は保持しています。");
        File.Delete(statePath);
        // The small cached icon is intentionally kept; never delete arbitrary files.
        notify(); return true;
    }

    State? LoadState()
    {
        if (!File.Exists(statePath)) return null;
        if (new FileInfo(statePath).Length > 32768) throw new InvalidDataException("アイコンの復元情報を確認できません。");
        var state = JsonSerializer.Deserialize<State>(File.ReadAllBytes(statePath));
        if (state == null || state.Version != 1 || state.InstalledValue != IconValue || state.Original == null ||
            state.Original.HasValue && (!state.Original.KeyExists || state.Original.Value == null || state.Original.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)) ||
            !state.Original.HasValue && state.Original.Value != null)
            throw new InvalidDataException("アイコンの復元情報が不正です。設定は変更しません。");
        return state;
    }
    void EnsureUnlinkedFolder()
    {
        for (DirectoryInfo? d = new(folder); d != null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("リンク経由のアイコン保存先は使用できません。");
        Directory.CreateDirectory(folder);
        foreach (string path in new[] { IconPath, statePath })
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("リンク経由の設定ファイルは使用できません。");
    }
    static bool IsInstalled(CcapIconValue current, State state) => current.KeyExists && current.HasValue && current.Kind == RegistryValueKind.String && current.Value == state.InstalledValue;
    static IOException Conflict() => new("別の操作でアイコン設定が変更されています。現在の設定を上書きしません。");
    static void ValidateIcon(byte[] data)
    {
        if (data.Length < 22 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2)) != 1)
            throw new InvalidDataException("アイコン形式が不正です。");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        if (count == 0 || 6 + count * 16 > data.Length) throw new InvalidDataException("アイコン情報が不正です。");
        for (int i = 0; i < count; i++)
        {
            var entry = data.AsSpan(6 + i * 16, 16);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]), offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if (length == 0 || offset < 6 + count * 16 || (ulong)offset + length > (ulong)data.Length) throw new InvalidDataException("アイコン画像が不正です。");
        }
    }
    static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
