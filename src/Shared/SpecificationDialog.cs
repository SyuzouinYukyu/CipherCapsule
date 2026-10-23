namespace CipherCapsule.UI;

// Isolated from generic result dialogs: only the specification needs clickable URLs.
public sealed class SpecificationDialog : AppForm
{
    public RichTextBox Content { get; }
    public Button CloseButton { get; }
    public SpecificationDialog(string content,Size size)
    {
        Text="暗号アルゴリズム仕様";Size=size;
        Content=new RichTextBox{Multiline=true,ReadOnly=true,DetectUrls=true,Dock=DockStyle.Fill,
            WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,Text=content,BackColor=SystemColors.Window};
        CloseButton=Button("閉じる",(_,_)=>Close());
        Content.LinkClicked+=(_,e)=>{string? warning=RepositoryLink.TryOpen(e.LinkText);if(warning!=null)MessageBox.Show(this,warning,"GitHubリンク",MessageBoxButtons.OK,MessageBoxIcon.Warning);};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12)};
        layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(Content,0,0);layout.Controls.Add(CloseButton,0,1);Controls.Add(layout);
        AcceptButton=CancelButton=CloseButton;ActiveControl=CloseButton;
        Shown+=(_,_)=>{Content.Select(0,0);Content.ScrollToCaret();ActiveControl=CloseButton;CloseButton.Focus();};
    }
}
