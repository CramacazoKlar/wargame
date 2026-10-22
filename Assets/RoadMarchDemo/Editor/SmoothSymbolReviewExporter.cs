using System.IO;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor {
 public static class SmoothSymbolReviewExporter {
  public static void Run(){foreach(var name in new[]{"Radar","Rocket"}){var symbols=Resources.Load<SmoothUnitSymbols>("Radar/"+name+"Symbols");int width=1200,height=570;var canvas=new Texture2D(width,height);var pixels=new Color[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.94f,.92f,.86f);
   System.Action<Sprite,int,int,int> draw=(sprite,left,bottom,displayWidth)=>{var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));int h=Mathf.RoundToInt(displayWidth*tex.height/(float)tex.width);for(int y=0;y<h;y++)for(int x=0;x<displayWidth;x++){var color=tex.GetPixelBilinear((x+.5f)/displayWidth,(y+.5f)/h);int at=(bottom+y)*width+left+x;pixels[at]=Color.Lerp(pixels[at],new Color(color.r,color.g,color.b,1),color.a);}Object.DestroyImmediate(tex);};
   draw(symbols.blue[4],65,300,300);draw(symbols.red[4],450,300,300);draw(symbols.neutral[4],835,300,300);
   int[] sizes={128,64,44,32,24,16};for(int i=0;i<sizes.Length;i++)draw(symbols.Select(sizes[i],"A"),75+i*190,170,sizes[i]);for(int i=0;i<sizes.Length;i++)draw(symbols.Select(sizes[i],"A"),50+i*190,32,140);
   canvas.SetPixels(pixels);canvas.Apply();string label=name=="Radar"?"雷达":"火箭兵";File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-19/c-desktop-wargame-9/outputs/"+label+"_实装军标尺寸对照.png",canvas.EncodeToPNG());Object.DestroyImmediate(canvas);
  }Debug.Log("SMOOTH_SYMBOL_REVIEW_PASS top=factions middle=actual128,64,44,32,24,16 bottom=enlarged");}
 }
}
