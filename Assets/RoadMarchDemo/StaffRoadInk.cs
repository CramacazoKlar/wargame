using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // All samples are anchored in road arclength, never screen pixels or frame time.
    public static class StaffRoadInk
    {
        public sealed class Stroke {public Vector2[] points;public float width,alpha;public bool dark,echo;}
        public sealed class Result {public readonly List<Stroke> strokes=new List<Stroke>();public int gaps,darkPasses,echoes;}
        public static Result Build(Vector2 a,Vector2 b,float separation,float thickness,float scale,int seed,Func<Vector2,bool> protectedPoint=null)
        {
            var result=new Result();float length=Vector2.Distance(a,b);if(length<.0001f)return result;var d=(b-a)/length;var n=new Vector2(-d.y,d.x);scale=Mathf.Max(.01f,scale);
            for(int sign=-1;sign<=1;sign+=2)
            {
                int slot=0;for(float arc=0;arc<length;slot++)
                {
                    float segment=(.08f+.10f*StaffMapCanvas.Hash(slot,sign,seed+9))*scale;float end=Mathf.Min(length,arc+segment);float mid=(arc+end)*.5f;var centre=a+d*mid;
                    bool protect=arc<.30f*scale||end>length-.30f*scale||(protectedPoint!=null&&protectedPoint(centre));
                    if(!protect&&StaffMapCanvas.Hash(slot,sign,seed+111)<.067f){result.gaps++;arc=end;continue;}
                    float pressure=StaffMapCanvas.Noise(mid/(scale*.36f),sign+5,seed+37);
                    var p=new Vector2[4];for(int i=0;i<p.Length;i++){float at=Mathf.Lerp(arc,end,i/(float)(p.Length-1));float wobble=(StaffMapCanvas.Noise(at/(scale*.23f),sign+8,seed)-.5f)*thickness*1.1f;p[i]=a+d*at+n*(sign*separation*.5f+wobble);}
                    result.strokes.Add(new Stroke{points=p,width=thickness*Mathf.Lerp(.74f,1.3f,pressure),alpha=Mathf.Lerp(.62f,.94f,pressure)});
                    if(StaffMapCanvas.Hash(slot,sign,seed+42)<.13f){result.strokes.Add(new Stroke{points=new[]{Vector2.Lerp(p[0],p[3],.17f),Vector2.Lerp(p[0],p[3],.76f)},width=thickness*1.08f,alpha=.48f,dark=true});result.darkPasses++;}
                    if(StaffMapCanvas.Hash(slot,sign,seed+78)<.46f){var echo=new Vector2[p.Length];for(int i=0;i<p.Length;i++)echo[i]=p[i]+n*(sign*thickness*.95f);result.strokes.Add(new Stroke{points=echo,width=thickness*.52f,alpha=.12f,echo=true});result.echoes++;}
                    arc=end;
                }
            }
            return result;
        }
    }
}
