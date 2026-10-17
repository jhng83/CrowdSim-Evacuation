"""Independent float32 port/reference checks, NOT execution of the C# implementation."""
import math, json, random
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon, Point, LineString
R = random.Random(27351)
f=np.float32
H=np.array([.75,.20],dtype=np.float32)
def axis(deg):
    t=f(f(deg)*f(math.pi)/f(180));a=np.array([f(math.cos(t)),f(math.sin(t))],dtype=np.float32)
    a[np.abs(a)<1e-6]=0;a/=np.linalg.norm(a)
    return np.array([a,[-a[1],a[0]]],dtype=np.float32)
def local(pt,c,a): return np.array([np.dot(pt-c,v) for v in a],dtype=np.float32)
def world(pt,c,a): return c+a[0]*pt[0]+a[1]*pt[1]
def poly(c,a): return Polygon([world(v*H,c,a) for v in np.array([[-1,-1],[1,-1],[1,1],[-1,1]],dtype=np.float32)])
def box(x,y,h):
    near,far=f(0),f(1)
    d=y-x
    for k in range(2):
        if abs(d[k])<1e-8:
            if x[k]<-h[k] or x[k]>h[k]:return False
        else:
            first=f((-h[k]-x[k])/d[k]);last=f((h[k]-x[k])/d[k])
            if first>last:first,last=last,first
            near=max(near,first);far=min(far,last)
            if near>far:return False
    return True

def sweep(x,y,c,a,m):
    x,y=local(x,c,a),local(y,c,a)
    if not box(x,y,H+m):return False
    if m==0 or box(x,y,H+np.array([m,0],dtype=np.float32)) or box(x,y,H+np.array([0,m],dtype=np.float32)):return True
    delta=y-x;ll=np.dot(delta,delta)
    for corner in [H,-H,H*np.array([1,-1]),H*np.array([-1,1])]:
        t=np.clip(np.dot(corner-x,delta)/ll,0,1) if ll>1e-12 else 0
        if np.dot(x+t*delta-corner,x+t*delta-corner)<=m*m:return True
    return False

def sat(c1,a1,c2,a2,tolerance=0):
    for v in list(a1)+list(a2):
        r1=sum(H[k]*abs(np.dot(a1[k],v)) for k in range(2));r2=sum(H[k]*abs(np.dot(a2[k],v)) for k in range(2))
        if r1+r2-abs(np.dot(c2-c1,v))<=tolerance:return False
    return True

counts={'swept_disc_vs_polygon':0,'point_clearance_vs_polygon':0,'sat_overlap_vs_polygon':0,'disc_separation':0,'flush_diagonal_contacts':0,'diagonal_overlap_rejections':0}
for i in range(5000):
    c=np.array([R.uniform(2,50),R.uniform(2,30)],dtype=np.float32);a=axis(R.uniform(0,360));p=poly(c,a)
    pt=np.array([R.uniform(-2,2),R.uniform(-2,2)],dtype=np.float32)+c
    end=np.array([R.uniform(-2,2),R.uniform(-2,2)],dtype=np.float32)+c;m=f(R.uniform(0,.45))
    actual=sweep(pt,end,c,a,m);distance=LineString([pt,end]).distance(p)
    assert actual==(distance<=m) or abs(distance-m)<2e-5,('sweep',i,distance,m)
    counts['swept_disc_vs_polygon']+=1
    outside=np.maximum(np.abs(local(pt,c,a))-H,0);actual=np.dot(outside,outside)<=m*m;distance=Point(pt).distance(p)
    assert actual==(distance<=m) or abs(distance-m)<2e-5,('contains',i,distance,m)
    counts['point_clearance_vs_polygon']+=1
    c2=c+np.array([R.uniform(-2,2),R.uniform(-2,2)],dtype=np.float32);a2=axis(R.uniform(0,360));p2=poly(c2,a2)
    actual=sat(c,a,c2,a2);area=p.intersection(p2).area
    assert actual==(area>1e-10) or area<1e-8 and p.distance(p2)<2e-5,('sat',i,area)
    counts['sat_overlap_vs_polygon']+=1
    q=world(np.array([R.uniform(-.9,.9),R.uniform(-.35,.35)],dtype=np.float32),c,a);r=f(.2)
    ql=local(q,c,a);closest=np.clip(ql,-H,H);diff=ql-closest;d2=np.dot(diff,diff)
    correction=np.zeros(2,dtype=np.float32)
    if d2>1e-12:
        if d2<r*r:
            d=f(math.sqrt(d2));corr=diff*((r-d+f(1e-5))/d);correction=a[0]*corr[0]+a[1]*corr[1]
    else:
        depths=H-np.abs(ql)+r;k=0 if depths[0]<depths[1] else 1
        correction=a[k]*(-1 if ql[k]<0 else 1)*(depths[k]+f(1e-5))
    after=q+correction
    assert Point(after).distance(p)>=float(r)-2e-5,('separation',i,Point(after).distance(p))
    counts['disc_separation']+=1
for degree in np.linspace(0,359,360):
    c=np.array([15,15],dtype=np.float32);a=axis(degree)
    for reverse in (False,True):
        a2=axis(degree+180 if reverse else degree)
        assert not sat(c,a,c+a[0]*f(1.5),a2,1e-4),('flush',degree,reverse)
        counts['flush_diagonal_contacts']+=1
        assert sat(c,a,c+a[0]*f(1.49),a2,1e-4),('overlap',degree,reverse)
        counts['diagonal_overlap_rejections']+=1
result={'verification_type':'independent Python float32 algorithm/reference check; not C# execution','seed':27351,'checks':counts,'total':sum(counts.values()),'result':'passed'}
Path('angle_geometry_results.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
