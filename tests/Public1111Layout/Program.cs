extern alias BuilderApp;
extern alias ViewerApp;
using System.Reflection;
using System.Text.Json;
using CipherCapsule.Core;
class Probe {
 [STAThread] static int Main(string[] args){
 var results=new List<object>();int failed=0;
 void Test(string n,Action a){try{a();results.Add(new{name=n,passed=true});}catch(Exception e){failed++;results.Add(new{name=n,passed=false,error=e.ToString()});}}
 void Check(bool b){if(!b)throw new Exception("assertion");}
 object Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(obj)!;
 string root=args[0];Directory.CreateDirectory(root);string a=Path.Combine(root,"v1.ccap"),b=Path.Combine(root,"v2.ccap");byte[] pw=Limits.Utf8.GetBytes("Layout1111-Dummy!");
 Container.GenerateCcap(a,"dummy",BodyKind.TEXT,[],pw,true,default);Container.GenerateCcap(b,"dummy",BodyKind.TEXT,[],pw,true,default,publicMessage:"日本語😀\n<script>https://example.com</script>");
 Test("Builder defaults and top public row (not displayed)",()=>{using var f=new BuilderApp::CipherCapsule.Builder.BuilderForm();var input=(TextBox)Field(f,"publicMessage");var table=(TableLayoutPanel)Field(f,"root");Check(table.GetPositionFromControl(input.Parent!).Row==0);Check(input.Multiline&&input.Text.Length==0);Check(((CheckBox)Field(f,"selfExtracting")).Checked&&((CheckBox)Field(f,"allowCopy")).Checked&&!((CheckBox)Field(f,"checksum")).Checked&&!((CheckBox)Field(f,"checksum256")).Checked);});
 foreach(float size in new[]{9f,12f,17f})Test("Authentication layout construction "+size+"pt (not displayed)",()=>{using var f=new ViewerApp::CipherCapsule.UI.PasswordDialog(b,true);f.SetSize(size);f.PerformLayout();var layout=f.Controls.OfType<TableLayoutPanel>().Single();Check(layout.Controls.Count==5);var area=(TableLayoutPanel)layout.Controls[0];var input=area.Controls.OfType<TextBox>().Single();Check(input.ReadOnly&&input.Multiline&&input.ScrollBars==ScrollBars.Vertical&&input.SelectionLength==0&&input.Text.Contains("<script>"));});
 Test("v1 authentication retains four existing rows",()=>{using var f=new ViewerApp::CipherCapsule.UI.PasswordDialog(a,true);Check(f.Controls.OfType<TableLayoutPanel>().Single().Controls.Count==4);});
 File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{passed=results.Count-failed,failed,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{results.Count-failed}/{failed}");return failed==0?0:1;
 }
}
