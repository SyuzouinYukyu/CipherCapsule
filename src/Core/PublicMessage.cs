using System.Globalization;
namespace CipherCapsule.Core;
public static class PublicMessage
{
    public const int MaxBytes=4096;
    public static string Normalize(string? text)
    {
        text??="";
        text=text.Replace("\r\n","\n").Replace('\r','\n');
        if(Limits.Utf8.GetByteCount(text)>MaxBytes)throw new ArgumentException("受信者向けメッセージはUTF-8換算4,096バイト以内にしてください。");
        foreach(char c in text)if((char.IsControl(c)&&c!='\n'&&c!='\t')||c is >= '\u202a' and <= '\u202e' or >= '\u2066' and <= '\u2069')throw new ArgumentException("受信者向けメッセージに使用できない制御文字があります。");
        return string.IsNullOrWhiteSpace(text)?"":text;
    }
    internal static string Decode(byte[] bytes)
    {
        string text=Limits.Utf8.GetString(bytes);
        if(text.Length==0||Normalize(text)!=text||string.IsNullOrWhiteSpace(text))throw new InvalidDataException("非暗号化メッセージの形式が不正です。");
        return text;
    }
}
