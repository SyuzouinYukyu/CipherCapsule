using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Buffers.Binary;
namespace CipherCapsule.Core;
public static class SafeHtml
{
    static readonly HashSet<string> Allowed=new("a p div span br hr h1 h2 h3 h4 h5 h6 b strong i em u s small sub sup pre code blockquote ul ol li table colgroup col thead tbody tfoot tr td th caption img font".Split(' '));
    static readonly HashSet<string> Drop=new("script style iframe frame frameset object embed svg math template form input button textarea select option link base meta noscript audio video source canvas".Split(' '));
    public static string? SafeUrl(string? value)
    {
        if(value==null||value.Length>4096||value.Any(char.IsControl))return null;
        return Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme is "http" or "https"&&!string.IsNullOrEmpty(u.Host)&&string.IsNullOrEmpty(u.UserInfo)?u.AbsoluteUri:null;
    }
    public static string Fragment(string html)
    {
        Limits.BodyText(html);using var doc=new HtmlParser(new HtmlParserOptions{IsScripting=false}).ParseDocument(html);
        var b=new StringBuilder();int images=0,nodes=0;if(doc.Body!=null)foreach(var n in doc.Body.ChildNodes)Render(n,b,0,ref images,ref nodes);return b.ToString();
    }
    public static string Sanitize(string html,float fontSize=12,bool allowCopy=true)
    {
        string copy=allowCopy?"":"*{-webkit-user-select:none!important;user-select:none!important}";
        return "<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-src 'none'; connect-src 'none'; object-src 'none'; script-src 'none'; sandbox allow-top-navigation-by-user-activation\"><style>body{font-family:Yu Gothic UI,Meiryo,sans-serif;font-size:"+Math.Clamp(fontSize,9,17).ToString(System.Globalization.CultureInfo.InvariantCulture)+"pt;overflow-wrap:anywhere;margin:18px}table{border-collapse:collapse}td,th{border:1px solid #aaa;padding:6px}img{max-width:100%;height:auto}pre{white-space:pre-wrap}"+copy+"</style></head><body>"+Fragment(html)+"</body></html>";
    }
    public static string ValidateEditor(string html,out bool warning)
    {
        Limits.BodyText(html);bool parseError=false;var parser=new HtmlParser();parser.Error+=(_,e)=>parseError=true;
        using var doc=parser.ParseDocument("<!doctype html><html><head></head><body>"+html+"</body></html>");
        warning=parseError||doc.All.Any(e=>Drop.Contains(e.LocalName)||e.Attributes.Any(a=>a.Name.StartsWith("on",StringComparison.OrdinalIgnoreCase))||e.LocalName=="a"&&e.HasAttribute("href")&&SafeUrl(e.GetAttribute("href"))==null||e.LocalName=="img"&&ImageData(e.GetAttribute("src"))==null);
        return Fragment(html);
    }
    static void Render(INode n,StringBuilder b,int depth,ref int images,ref int nodes)
    {
        if(depth>100||++nodes>150000)throw new InvalidDataException("HTMLの階層または要素数が多すぎます。");
        if(n is IText t){b.Append(WebUtility.HtmlEncode(t.Data));return;}
        if(n is not IElement e||e.NamespaceUri!="http://www.w3.org/1999/xhtml")return;
        string tag=e.LocalName;if(Drop.Contains(tag))return;
        if(!Allowed.Contains(tag)){foreach(var c in e.ChildNodes)Render(c,b,depth+1,ref images,ref nodes);return;}
        if(tag=="img")
        {
            string? src=ImageData(e.GetAttribute("src"));if(src==null)return;images+=src.Length;if(images>2*1024*1024)throw new InvalidDataException("埋込み画像の合計容量が大きすぎます。");
            b.Append("<img src=\"").Append(src).Append("\" alt=\"").Append(WebUtility.HtmlEncode(e.GetAttribute("alt")??"")).Append('"');
            foreach(string a in new[]{"width","height"})if(int.TryParse(e.GetAttribute(a),out int v)&&v is >0 and <=4096)b.Append(' ').Append(a).Append("=\"").Append(v).Append('"');
            string style=Style(e.GetAttribute("style")??"");if(style.Length>0)b.Append(" style=\"").Append(WebUtility.HtmlEncode(style)).Append('"');b.Append('>');return;
        }
        b.Append('<').Append(tag);
        if(tag=="a"&&SafeUrl(e.GetAttribute("href")) is string url)b.Append(" href=\"").Append(WebUtility.HtmlEncode(url)).Append("\" rel=\"noreferrer noopener\"");
        if(e.HasAttribute("title"))b.Append(" title=\"").Append(WebUtility.HtmlEncode(e.GetAttribute("title"))).Append('"');
        string css=Style(e.GetAttribute("style")??"");if(tag=="font"&&Color(e.GetAttribute("color")??""))css+="color:"+e.GetAttribute("color")+";";
        if(css.Length>0)b.Append(" style=\"").Append(WebUtility.HtmlEncode(css)).Append('"');
        foreach(string a in new[]{"colspan","rowspan"})if(tag is "td" or "th"&&int.TryParse(e.GetAttribute(a),out int v)&&v is >=1 and <=100)b.Append(' ').Append(a).Append("=\"").Append(v).Append('"');
        b.Append('>');foreach(var c in e.ChildNodes)Render(c,b,depth+1,ref images,ref nodes);if(tag is not("br" or "hr" or "col"))b.Append("</").Append(tag).Append('>');
    }
    static bool Color(string s)=>Regex.IsMatch(s,@"\A(?:#[0-9a-fA-F]{3}|#[0-9a-fA-F]{6}|[a-zA-Z]{3,20}|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}(?:\s*,\s*(?:0|1|0?\.\d{1,3}))?\s*\))\z");
    static string Style(string input)
    {
        var b=new StringBuilder();foreach(string d in input.Split(';'))
        {
            string[] pair=d.Split(':',2);if(pair.Length!=2)continue;string k=pair[0].Trim().ToLowerInvariant(),v=pair[1].Trim();string lower=v.ToLowerInvariant();
            bool ok=k switch {
                "color" or "background-color"=>Color(v),"text-align"=>lower is "left" or "right" or "center" or "justify",
                "font-weight"=>lower is "normal" or "bold" or "400" or "700","font-style"=>lower is "normal" or "italic",
                "text-decoration"=>lower is "none" or "underline" or "line-through" or "underline line-through",
                "white-space"=>lower is "normal" or "pre" or "pre-wrap","font-size"=>Regex.IsMatch(v,@"\A(?:[1-9]|[1-8][0-9]|9[0-6])(?:px|pt)\z"),
                "font-family"=>v.Length<=200&&Regex.IsMatch(v,@"\A[a-zA-Z0-9\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana} ,\-'""\u3000]+\z"),
                "margin-left" or "padding-left"=>Regex.IsMatch(v,@"\A(?:0|[1-3]?[0-9]{1,2}|400)px\z"),
                "width" or "height"=>Regex.IsMatch(v,@"\A(?:[1-9][0-9]{0,2}|[1-3][0-9]{3}|40[0-8][0-9]|409[0-6])px\z|\A(?:[1-9][0-9]?|100)%\z|\Aauto\z"),
                "border-collapse"=>lower is "collapse" or "separate","border-width"=>Regex.IsMatch(v,@"\A[0-9]px\z"),
                "border-style"=>lower is "solid" or "dashed" or "dotted" or "none","border-color"=>Color(v),
                "border"=>Regex.IsMatch(v,@"\A[0-9]px (?:solid|dashed|dotted) #[a-fA-F0-9]{6}\z"),_=>false};
            if(ok)b.Append(k).Append(':').Append(v).Append(';');
        }return b.ToString();
    }
    public static bool ImageHeader(ReadOnlySpan<byte> b,bool png,out int width,out int height)
    {
        width=height=0;
        if(png){if(b.Length<33||!b[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||BinaryPrimitives.ReadUInt32BigEndian(b[8..])!=13||!b.Slice(12,4).SequenceEqual("IHDR"u8))return false;width=BinaryPrimitives.ReadInt32BigEndian(b[16..]);height=BinaryPrimitives.ReadInt32BigEndian(b[20..]);}
        else {
            if(b.Length<4||b[0]!=255||b[1]!=216)return false;int p=2;bool found=false;
            while(p<b.Length){if(b[p++]!=255)return false;while(p<b.Length&&b[p]==255)p++;if(p>=b.Length)return false;byte m=b[p++];if(m is 0xd9 or 0xda)break;if(m==0x01||m is >=0xd0 and <=0xd7)continue;if(p>b.Length-2)return false;int len=BinaryPrimitives.ReadUInt16BigEndian(b[p..]);if(len<2||len>b.Length-p)return false;
                if(m is 0xc0 or 0xc1 or 0xc2){if(len<8)return false;height=BinaryPrimitives.ReadUInt16BigEndian(b[(p+3)..]);width=BinaryPrimitives.ReadUInt16BigEndian(b[(p+5)..]);found=true;break;}
                if(m is >=0xc0 and <=0xcf&&m is not(0xc4 or 0xc8 or 0xcc))return false;p+=len;
            }if(!found)return false;
        }
        return width is >0 and <=4096&&height is >0 and <=4096&&(long)width*height<=4*1024*1024;
    }
    public static string? ImageData(string? src)
    {
        if(src==null||src.Length>360000)return null;bool png=src.StartsWith("data:image/png;base64,",StringComparison.OrdinalIgnoreCase);string prefix=png?"data:image/png;base64,":src.StartsWith("data:image/jpeg;base64,",StringComparison.OrdinalIgnoreCase)?"data:image/jpeg;base64,":"";if(prefix.Length==0)return null;
        try {
            byte[] bytes=Convert.FromBase64String(src[prefix.Length..]);if(bytes.Length>256*1024||!ImageHeader(bytes,png,out int w,out int h))return null;
            using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream,false,true);if(image.Width!=w||image.Height!=h||image.RawFormat.Guid!=(png?ImageFormat.Png.Guid:ImageFormat.Jpeg.Guid))return null;
            using var clean=new Bitmap(image);using var output=new MemoryStream();clean.Save(output,ImageFormat.Png);if(output.Length>256*1024)return null;return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());
        }catch{return null;}
    }
}
