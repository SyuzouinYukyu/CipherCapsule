from PIL import Image, ImageDraw
from pathlib import Path
root=Path(__file__).resolve().parents[1]
for kind in ['builder','viewer']:
    im=Image.new('RGBA',(1024,1024)); d=ImageDraw.Draw(im)
    def box(v): return tuple(x*4 for x in v)
    navy='#14263e'; teal='#1ab4a6'; gold='#ffcf52'
    d.ellipse(box((8,8,248,248)),fill=navy)
    capsule=Image.new('RGBA',im.size); c=ImageDraw.Draw(capsule)
    c.rounded_rectangle(box((35,65,221,191)),radius=63*4,fill=teal)
    c.line(box((128,69,128,187)),fill='white',width=9*4)
    im.alpha_composite(capsule.rotate(28,Image.Resampling.BICUBIC));d=ImageDraw.Draw(im)
    d.arc(box((100,85,156,146)),180,360,fill='white',width=12*4)
    d.rounded_rectangle(box((91,116,165,181)),radius=8*4,fill=gold)
    d.ellipse(box((120,130,136,146)),fill=navy);d.rectangle(box((125,141,131,161)),fill=navy)
    if kind=='viewer':d.polygon([(183*4,184*4),(183*4,222*4),(216*4,203*4)],fill='white')
    im.save(root/f'assets/{kind}.ico',sizes=[(s,s) for s in [16,24,32,48,64,128,256]])
print('builder.ico / viewer.ico created')
