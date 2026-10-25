using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CipherCapsule.Core;

/// <summary>CipherCapsule's own text wrapper. This is not OpenPGP armor.</summary>
public static class AsciiArmor
{
    public const string Begin="-----BEGIN CIPHERCAPSULE MESSAGE-----";
    public const string End="-----END CIPHERCAPSULE MESSAGE-----";
    public const int MaxTextBytes=16_384,MaxArmorLength=256*1024,MaxBinaryBytes=192*1024;
    static InvalidDataException Invalid()=>new("暗号メッセージの形式・Version・サイズを確認してください。TEXT本文・添付なしの形式だけに対応します。");
    public static void ValidateText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if(text.Length is 0 or >MaxTextBytes)throw new ArgumentException("本文はUTF-8で1～16,384バイトにしてください。");
        int n=Limits.Utf8.GetByteCount(text);
        if(n is <1 or >MaxTextBytes)throw new ArgumentException("本文はUTF-8で1～16,384バイトにしてください。");
    }
    public static string Encode(byte[] binary)
    {
        ValidateContainer(binary);
        string base64=Convert.ToBase64String(binary);var result=new StringBuilder(base64.Length+base64.Length/64+100);
        result.Append(Begin).Append("\nVersion: 1\n\n");
        for(int i=0;i<base64.Length;i+=64)result.Append(base64,i,Math.Min(64,base64.Length-i)).Append('\n');
        result.Append('\n').Append(End);
        if(result.Length>MaxArmorLength)throw Invalid();
        return result.ToString();
    }
    public static byte[] Decode(string armor)
    {
        ArgumentNullException.ThrowIfNull(armor);
        // Bound before Trim/Split/Replace or any native KDF.
        if(armor.Length is 0 or >MaxArmorLength)throw Invalid();
        string text=armor.Trim(' ','\t','\r','\n').Replace("\r\n","\n",StringComparison.Ordinal);
        if(text.Contains('\r'))throw Invalid();
        string[] lines=text.Split('\n');
        if(lines.Length<6||lines[0]!=Begin||lines[1]!="Version: 1"||lines[2]!=""||lines[^2]!=""||lines[^1]!=End)throw Invalid();
        var encoded=new StringBuilder();
        for(int i=3;i<lines.Length-2;i++)
        {
            string line=lines[i];bool last=i==lines.Length-3;
            if(line.Length is <1 or >64||(!last&&line.Length!=64)||(last&&line.Length%4!=0))throw Invalid();
            for(int c=0;c<line.Length;c++)
            {
                char ch=line[c];
                if(ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/')continue;
                if(ch=='='&&last&&c>=line.Length-2)continue;
                throw Invalid();
            }
            encoded.Append(line);
        }
        if(encoded.Length==0||encoded.Length>4*((MaxBinaryBytes+2)/3))throw Invalid();
        byte[] bytes;
        try{bytes=Convert.FromBase64String(encoded.ToString());}catch(FormatException){throw Invalid();}
        try{
            if(bytes.Length>MaxBinaryBytes||Convert.ToBase64String(bytes)!=encoded.ToString())throw Invalid();
            ValidateContainer(bytes);return bytes;
        }catch{Sodium.Wipe(bytes);throw;}
    }
    public static void ValidateContainer(byte[] b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if(b.Length<256||b.Length>MaxBinaryBytes)throw Invalid();
        uint U(int n)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(n));
        long L(int n)=>BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(n));
        int f=b.Length-64;
        if(!b.AsSpan(0,8).SequenceEqual("CCAPDAT1"u8)||U(8)!=1||U(12)!=1||L(16)!=3||L(24)!=256L*1024*1024||L(144)!=176||L(152)<16||L(152)!=b.Length-240||U(160)!=0||U(164)!=Limits.Chunk||L(168)!=0)throw Invalid();
        if(!b.AsSpan(f,8).SequenceEqual("CCAPEND1"u8)||U(f+8)!=1||U(f+12)!=0||L(f+16)!=0||L(f+24)!=b.Length-64||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(b.AsSpan(f,32)),b.AsSpan(f+32,32)))throw Invalid();
    }
}

/// <summary>Uses the existing authenticated CCAP writer and reader entirely in memory.</summary>
public static class ArmorMessages
{
    public static string Encrypt(string text,byte[] password,CancellationToken ct)
    {
        AsciiArmor.ValidateText(text);ct.ThrowIfCancellationRequested();
        using var memory=new MemoryStream();byte[]? bytes=null;
        try
        {
            Container.GenerateCcap(memory,text,BodyKind.TEXT,Array.Empty<string>(),password,ct,allowCopy:true);
            ct.ThrowIfCancellationRequested();
            if(memory.Length>AsciiArmor.MaxBinaryBytes)throw new InvalidDataException("暗号メッセージの容量上限を超えました。");
            bytes=memory.ToArray();return AsciiArmor.Encode(bytes);
        }
        finally { Sodium.Wipe(bytes);if(memory.TryGetBuffer(out var buffer))Sodium.Wipe(buffer.Array); }
    }
    public static string Decrypt(string armor,byte[] password,CancellationToken ct)
    {
        byte[] bytes=AsciiArmor.Decode(armor); // Structure/size bounds precede KDF.
        try
        {
            ct.ThrowIfCancellationRequested();using var memory=new MemoryStream(bytes,writable:false);
            using var capsule=Container.Unlock(memory,password,ct,ccapOnly:true,leaveOpen:true);
            if(capsule.Manifest.Kind!=BodyKind.TEXT||capsule.Manifest.Attachments.Count!=0)throw new InvalidDataException("暗号メッセージはTEXT本文・添付なしだけに対応します。");
            AsciiArmor.ValidateText(capsule.Manifest.Body);ct.ThrowIfCancellationRequested();return capsule.Manifest.Body;
        }
        finally { Sodium.Wipe(bytes); }
    }
    // Source compatibility for callers of v1.1.9. No path access occurs.
    public static string Encrypt(string text,byte[] password,string temporaryRoot,CancellationToken ct)=>Encrypt(text,password,ct);
    public static string Decrypt(string armor,byte[] password,string temporaryRoot,CancellationToken ct)=>Decrypt(armor,password,ct);
}

/// <summary>Explicit UI state: generated ciphertext only; never copies plaintext.</summary>
public sealed class ArmorMessageState
{
    string generated="";
    public string PlainResult {get;private set;}="";
    public bool CanCopy(string displayed)=>generated.Length>0&&string.Equals(generated,displayed,StringComparison.Ordinal);
    public void CipherChanged(){generated="";PlainResult="";}
    public void BeginOperation(){generated="";PlainResult="";}
    public void EncryptionSucceeded(string armor){var bytes=AsciiArmor.Decode(armor);Sodium.Wipe(bytes);generated=armor;PlainResult="";}
    public void DecryptionSucceeded(string text){AsciiArmor.ValidateText(text);PlainResult=text;}
    public string CopyGenerated(string displayed)=>CanCopy(displayed)?generated:throw new InvalidOperationException("生成済みの暗号文がありません。");
    public static void ValidatePaste(string text){var bytes=AsciiArmor.Decode(text);Sodium.Wipe(bytes);}
}
