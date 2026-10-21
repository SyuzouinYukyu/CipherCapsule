using System.Text.RegularExpressions;
namespace CipherCapsule.Core;
public static class ClipboardHtml
{
    public static string Encode(string fragment)
    {
        const string template="Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string before="<html><body><!--StartFragment-->",after="<!--EndFragment--></body></html>";
        int start=string.Format(template,0,0,0,0).Length,fragmentStart=start+Limits.Utf8.GetByteCount(before),fragmentEnd=fragmentStart+Limits.Utf8.GetByteCount(fragment),end=fragmentEnd+Limits.Utf8.GetByteCount(after);
        return string.Format(template,start,end,fragmentStart,fragmentEnd)+before+fragment+after;
    }
    public static string Decode(string html)
    {
        if(Limits.Utf8.GetByteCount(html)>Limits.Body+4096)throw new ArgumentException("クリップボードのHTMLが8MiBを超えています。");
        var start=Regex.Match(html,@"(?m)^StartFragment:(\d+)");var end=Regex.Match(html,@"(?m)^EndFragment:(\d+)");
        if(start.Success&&end.Success&&int.TryParse(start.Groups[1].Value,out int a)&&int.TryParse(end.Groups[1].Value,out int b))
        {byte[] bytes=Limits.Utf8.GetBytes(html);if(a>=0&&b>=a&&b<=bytes.Length)return Limits.Utf8.GetString(bytes,a,b-a);throw new ArgumentException("クリップボードのHTML範囲が不正です。");}
        return html;
    }
}
