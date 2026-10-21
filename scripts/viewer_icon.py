from PIL import Image,ImageDraw
from pathlib import Path
root=Path(__file__).resolve().parents[1]
im=Image.new('RGBA',(1024,1024));d=ImageDraw.Draw(im)
def b(a):return tuple(x*4 for x in a)
navy='#173049';gold='#f3bc34'
d.rounded_rectangle(b((6,32,250,224)),radius=20*4,fill=navy)
d.rounded_rectangle(b((13,40,243,215)),radius=13*4,fill='white')
d.polygon([(14*4,207*4),(95*4,126*4),(159*4,126*4),(241*4,207*4)],fill='#e5eaf0')
d.line([(15*4,48*4),(128*4,142*4),(240*4,48*4)],fill='#697d91',width=7*4,joint='curve')
d.arc(b((142,104,208,180)),180,360,fill=navy,width=18*4)
d.arc(b((145,106,205,176)),180,360,fill=gold,width=10*4)
d.rounded_rectangle(b((128,148,222,228)),radius=12*4,fill=navy)
d.rounded_rectangle(b((134,152,216,222)),radius=8*4,fill=gold)
d.ellipse(b((167,169,183,185)),fill=navy);d.rectangle(b((172,180,178,202)),fill=navy)
im.save(root/'assets/viewer.png');im.save(root/'assets/viewer.ico',sizes=[(s,s) for s in [16,24,32,48,64,128,256]])
