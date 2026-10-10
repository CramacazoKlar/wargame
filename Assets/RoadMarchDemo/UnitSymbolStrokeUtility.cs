using UnityEngine;
namespace CampaignDemo {
 public static class UnitSymbolStrokeUtility {
  // Grow ink one source pixel on either side; pale fill and transparency stay intact.
  public static Color[] Thicken(Color[] input,int width,int height) {
   var result=(Color[])input.Clone();
   for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
    Color ink=Color.clear;float best=0;
    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
     int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=width||yy>=height)continue;
     var c=input[yy*width+xx];float lo=Mathf.Min(c.r,Mathf.Min(c.g,c.b)),hi=Mathf.Max(c.r,Mathf.Max(c.g,c.b));
     if(c.a<=0||(lo>.55f&&hi-lo<.4f))continue;
     if(hi-lo<.035f)c=new Color(0,0,0,c.a*(1-hi));
     if(c.a>best){best=c.a;ink=c;}
    }
    if(best<=0)continue;var original=input[y*width+x];float alpha=best+original.a*(1-best);
    result[y*width+x]=new Color((ink.r*best+original.r*original.a*(1-best))/alpha,(ink.g*best+original.g*original.a*(1-best))/alpha,(ink.b*best+original.b*original.a*(1-best))/alpha,alpha);
   }
   return result;
  }
 }
}
