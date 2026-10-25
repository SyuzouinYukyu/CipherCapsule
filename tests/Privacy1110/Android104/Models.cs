using System.Text;

namespace CipherCapsule.Core;
public enum BodyKind { TEXT, HTML }
public sealed class AttachmentInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public long Offset { get; set; }
    public long CipherLength { get; set; }
    public string Sha256 { get; set; } = "";
}
public sealed class Manifest
{
    public BodyKind Kind { get; set; }
    public string Body { get; set; } = "";
    public bool AllowCopy { get; set; } = true;
    public List<AttachmentInfo> Attachments { get; set; } = [];
}
public sealed record CapsuleProgress(string Stage, long Done, long Total);
public static class Limits
{
    public const int Body = 8 * 1024 * 1024, Chunk = 1024 * 1024, Count = 100;
    public const long File = 256L * 1024 * 1024, Total = 512L * 1024 * 1024;
    public static readonly UTF8Encoding Utf8 = new(false, true);
    public static void Password(string p, bool creation = true)
    {
        int n = p.EnumerateRunes().Count();
        if (n < 1 || n > 256) throw new ArgumentException("パスワードは空欄にできません。256文字以内で指定してください。長いパスフレーズを推奨します。");
        _ = Utf8.GetByteCount(p);
    }
    public static void BodyText(string body) { if (Utf8.GetByteCount(body) > Body) throw new ArgumentException("本文はUTF-8換算8MiB以内にしてください。"); }
    public static void FileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || name.Any(char.IsControl) || name.Contains('/') || name.Contains('\\')) throw new InvalidDataException("安全でない添付ファイル名です。");
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem) || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789¹²³".Contains(stem[3]))) throw new InvalidDataException("Windows予約名は使用できません。");
    }
    public static void Inputs(IReadOnlyList<string> paths)
    {
        if (paths.Count > Count) throw new ArgumentException("添付は100ファイルまでです。");
        long total = 0;
        foreach (string path in paths)
        {
            var f = new FileInfo(path); FileName(f.Name);
            if (!f.Exists || (f.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new IOException("通常のファイルのみ登録できます。フォルダー・リンクは非対応です。");
            if (f.Length > File) throw new ArgumentException("単一添付は256MiBまでです。");
            total = checked(total + f.Length);
        }
        if (total > Total) throw new ArgumentException("添付合計は512MiBまでです。");
    }
}
public static class AtomicFile
{
    public static string Temporary(string dest) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dest))!, ".cc-" + Guid.NewGuid().ToString("N") + ".tmp");
    public static void Commit(string temp, string dest, bool overwrite)
    {
        if (System.IO.File.Exists(dest)) { if (!overwrite) throw new IOException("出力先が既に存在します。"); System.IO.File.Replace(temp, dest, null); }
        else System.IO.File.Move(temp, dest);
    }
    public static void Delete(string? path) { try { if (path != null) System.IO.File.Delete(path); } catch { } }
}
