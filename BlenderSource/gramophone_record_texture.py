# 蓄音機のレコードの上面テクスチャ：みぞ＋光の帯（回っているのが分かる）＋まんなかのラベルに空の太陽（FuwaGradientSky と同じ形）
import numpy as np, sys
from PIL import Image
N=1024; SS=3
M=N*SS
y,x=np.mgrid[0:M,0:M]
u=(x+0.5)/M*2-1; v=1-(y+0.5)/M*2          # -1..1、+v が上
r=np.sqrt(u*u+v*v); ang=np.arctan2(v,u)
def mix(a,b,t): t=np.clip(t,0,1)[...,None]; return a*(1-t)+b*t
aa=2.0/M*1.5
def step(e,x): return np.clip((x-e)/aa+0.5,0,1)
C=lambda *c: np.array(c,dtype=np.float32)
base=C(0.22,0.22,0.32)
col=np.zeros((M,M,3),np.float32)+base
# ふち（少し濃く）
col=mix(col,C(0.16,0.16,0.24),step(0.965,r))
# みぞ：細い明るい線（0.36〜0.95）、曲の間のすき間を2本
LR=0.32
grooveZone=(r>LR+0.04)&(r<0.95)
g=0.5+0.5*np.cos(r*2*np.pi*42)
g=(g>0.82).astype(np.float32)
gap=(np.abs(r-0.58)<0.012)|(np.abs(r-0.78)<0.012)
gl=np.where(grooveZone&~gap,g,0)
col=mix(col,C(0.30,0.30,0.42),gl*0.9)
# すき間の線はつるっと少し明るい
col=mix(col,C(0.27,0.27,0.38),gap&grooveZone)
# 光の帯：向かい合う2つの扇形（回ると帯が回る）
sheen=np.exp(-((np.angle(np.exp(1j*(ang-0.6))*1)) ** 2)/0.09)+np.exp(-((np.angle(np.exp(1j*(ang-0.6-np.pi)))) ** 2)/0.09)
sheen=sheen*grooveZone
col=mix(col,C(0.58,0.59,0.76),sheen*0.85*(0.55+0.45*gl))
# ラベル：空色
lab=1-step(LR,r)
labCol=mix(C(0.60,0.83,1.0)[None,None,:]+np.zeros_like(col),C(0.45,0.72,0.98),np.clip(r/LR,0,1)**2)
labRim=step(LR*0.93,r)
labCol=mix(labCol,C(1,1,1),labRim*0.9)
col=mix(col,labCol,lab)
# 太陽（FuwaGradientSky：トゲ10本・長さ1.75R・根元の半幅0.3R・円のふち0.84R）
R=LR*0.88/1.75
rho=r; phi=ang+np.pi/2
cnt=10; sector=2*np.pi/cnt
a=(np.mod(phi/sector,1)-0.5)*sector
arc=np.abs(a)*rho
r0=R*1.18; r1=R*1.75
tr=np.clip((rho-r0)/(r1-r0),0,1)
hw=R*0.3*(1-tr)+R*0.06*tr
ray=(1-step(hw,arc))*step(r0,rho)*(1-step(r1,rho))
tip=np.sqrt(arc**2+(rho-r1)**2)
ray=np.maximum(ray,(1-step(R*0.06,tip))*(rho>=r0))
sunC=C(1,0.97,0.88)
col=mix(col,C(1,0.72,0.3)*sunC,ray)
disc=1-step(R,rho)
rim=step(R*0.84,rho)
dc=mix(C(1,0.93,0.55)[None,None,:]+np.zeros_like(col),C(1,0.72,0.3),rim)*sunC
col=mix(col,dc,disc)
# 顔
fx=u/R; fy=v/R
ex=np.abs(fx)-0.3; ey=np.clip(fy,0.02,0.3)
eye=1-step(0.065,np.sqrt(ex**2+(fy-ey)**2)*R)/1  # aa in uv
eye=1-np.clip((np.sqrt(ex**2+(fy-ey)**2)-0.065)*R/aa+0.5,0,1)
cheek=1-np.clip((np.sqrt((np.abs(fx)-0.52)**2+(fy+0.16)**2)-0.15)*R/aa+0.5,0,1)
col=mix(col,C(1,0.55,0.62)*sunC,cheek*0.85*disc)
col=mix(col,C(0.38,0.22,0.12)*sunC,eye*disc)
img=(np.clip(col,0,1)*255).astype(np.uint8)
im=Image.fromarray(img).resize((N,N),Image.LANCZOS)
im.save(sys.argv[1])
print('ok')
