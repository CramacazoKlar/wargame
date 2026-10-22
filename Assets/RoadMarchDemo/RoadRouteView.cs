using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampaignDemo
{
    // Visuals only: knows nothing about input, selection or movement.
    public sealed class RoadRouteView : MonoBehaviour
    {
        readonly List<Vector3> arrowPoints=new List<Vector3>(),arrowResult=new List<Vector3>(),arrowSpine=new List<Vector3>(),arrowLeft=new List<Vector3>(),arrowRight=new List<Vector3>();Vector3[] arrowArray=new Vector3[0];
        LineRenderer line;
        Material ownedMaterial;
        RoadMarchSettings arrowSettings;Camera arrowCamera;bool useArrowColor=true;
        public void ConfigureArrow(RoadMarchSettings settings,Camera camera=null,bool overrideColor=true){arrowSettings=settings;arrowCamera=camera;useArrowColor=overrideColor;}
        public void ShowArrow(IList<RoadPoint> points,bool drawHead=true)
        {
            if(points==null||points.Count<2){Clear();return;}
            var camera=arrowCamera!=null?arrowCamera:Camera.main;float units=camera!=null&&camera.orthographic?2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight):0;
            var contour=ArrowContour(points,Mathf.Max(.14f,units*6),Mathf.Max(.30f,units*12),Mathf.Max(.28f,units*10),drawHead);
            line.loop=false;line.numCornerVertices=0;line.numCapVertices=0;line.startWidth=line.endWidth=Mathf.Max(.025f,units*1.4f);if(useArrowColor)SetColor(StaffMapStyle.Blue);line.positionCount=contour.Count;line.SetPositions(contour.ToArray());
        }
        public static List<Vector3> StraightArrowContour(IList<RoadPoint> route,float width,float headWidth,float headLength,bool drawHead=true)
        {
            if(route==null||route.Count<2)return new List<Vector3>();
            return ArrowContour(new[]{route[0],route[route.Count-1]},width,headWidth,headLength,drawHead);
        }
        public static List<Vector3> ArrowContour(IList<RoadPoint> route,float shaftWidth,float headWidth,float headLength,bool drawHead=true)
        {return BuildArrowContour(route,shaftWidth,headWidth,headLength,new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),drawHead);}
        static List<Vector3> BuildArrowContour(IList<RoadPoint> route,float shaftWidth,float headWidth,float headLength,List<Vector3> points,List<Vector3> result,List<Vector3> spine,List<Vector3> left,List<Vector3> right,bool drawHead)
        {
            points.Clear();result.Clear();spine.Clear();left.Clear();right.Clear();if(route!=null)foreach(var p in route){var v=new Vector3((float)p.X,(float)p.Y,0);if(points.Count==0||Vector3.Distance(points[points.Count-1],v)>.00001f)points.Add(v);}if(points.Count<2)return result;
            float total=0;for(int i=1;i<points.Count;i++)total+=Vector3.Distance(points[i-1],points[i]);if(total<=.0001f)return result;
            float head=drawHead?Mathf.Min(Mathf.Max(.001f,headLength),total*.4f):0,half=Mathf.Min(Mathf.Max(.001f,shaftWidth)/2,total*.12f),wing=Mathf.Min(Mathf.Max(half*2,headWidth/2),total*.25f);float remain=total-head;spine.Add(points[0]);
            for(int i=1;i<points.Count;i++){float length=Vector3.Distance(points[i-1],points[i]);if(remain<=length){spine.Add(Vector3.Lerp(points[i-1],points[i],remain/length));break;}spine.Add(points[i]);remain-=length;}
            Vector3 tip=points[points.Count-1],basePoint=spine[spine.Count-1],direction=(drawHead?tip-basePoint:points[points.Count-1]-points[points.Count-2]).normalized,normal=new Vector3(-direction.y,direction.x);
            for(int i=0;i<spine.Count;i++){Vector3 before=i==0?(spine[1]-spine[0]).normalized:(spine[i]-spine[i-1]).normalized,after=i==spine.Count-1?direction:(spine[i+1]-spine[i]).normalized;Vector3 n0=new Vector3(-before.y,before.x),n1=new Vector3(-after.y,after.x),join=(n0+n1).normalized;if(join.sqrMagnitude<.0001f)join=n1;float dot=Mathf.Abs(Vector3.Dot(join,n1));float offset=dot>.001f?Mathf.Min(half/dot,half*3):half;left.Add(spine[i]+join*offset);right.Add(spine[i]-join*offset);}
            result.AddRange(left);if(drawHead){result.Add(basePoint+normal*wing);result.Add(tip);result.Add(basePoint-normal*wing);}for(int i=right.Count-1;i>=0;i--)result.Add(right[i]);return result;
        }
        public void Initialize(float width,int order,Color color)
        {
            line=GetComponent<LineRenderer>();
            if(line==null) line=gameObject.AddComponent<LineRenderer>();
            var shader=Shader.Find("Sprites/Default");
            if(shader==null) throw new InvalidOperationException("Sprites/Default shader missing.");
            ownedMaterial=new Material(shader); line.sharedMaterial=ownedMaterial;
            line.startWidth=line.endWidth=width; line.sortingOrder=order;
            line.useWorldSpace=true; line.numCapVertices=4; line.numCornerVertices=4;
            SetColor(color); Clear();
        }
        public void SetColor(Color c) { line.startColor=line.endColor=c; }
        public void Show(IList<RoadPoint> points)
        {
            line.loop=false;
            line.positionCount=points.Count;
            for(int i=0;i<points.Count;i++) line.SetPosition(i,new Vector3((float)points[i].X,(float)points[i].Y,0));
        }
        public void Clear() { if(line!=null) line.positionCount=0; }
        void OnDestroy() { if(ownedMaterial!=null){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);} }
        public static RoadRouteView Create(Transform parent,string name,float width,int order,Color color)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            var view=go.AddComponent<RoadRouteView>(); view.Initialize(width,order,color);var setup=FindObjectOfType<RoadMarchSceneSetup>();if(setup!=null)view.ConfigureArrow(setup.settings,setup.mapCamera);return view;
        }
    }
}



