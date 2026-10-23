namespace CipherCapsule.UI;

// Keeps ordinary TextBox selection/copy behavior while avoiding SelectAll on first focus.
public sealed class ReadOnlyDialog : AppForm
{
    public TextBox Content { get; }
    public Button CloseButton { get; }
    public ReadOnlyDialog(string title,string content,Size size,bool bothScrollBars=false)
    {
        Text=title;Size=size;
        Content=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Text=content,
            ScrollBars=bothScrollBars?ScrollBars.Both:ScrollBars.Vertical,WordWrap=!bothScrollBars};
        CloseButton=Button("閉じる",(_,_)=>Close());
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12)};
        layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(Content,0,0);layout.Controls.Add(CloseButton,0,1);Controls.Add(layout);
        AcceptButton=CancelButton=CloseButton;ActiveControl=CloseButton;
        Shown+=(_,_)=>{Content.Select(0,0);Content.ScrollToCaret();ActiveControl=CloseButton;CloseButton.Focus();};
    }
}
