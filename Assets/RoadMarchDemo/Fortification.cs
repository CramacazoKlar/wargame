using UnityEngine;
namespace CampaignDemo
{
    public sealed class Fortification:MonoBehaviour
    {
        public string ownerId;
        [Min(0)] public int level;
        [Min(.02f)] public float radius=.3f;
        [Min(.01f)] public float dismantleSeconds=15;
        [Min(.01f)] public float legacyHealth=200;
        public Vector3[] linePoints=new Vector3[0];
        public Vector3 engineerSide;
        public float builtLengthMeters;
        public string lineGroupId;
        public float WorldLength {get{float length=0;if(linePoints!=null)for(int i=1;i<linePoints.Length;i++)length+=Vector2.Distance(linePoints[i-1],linePoints[i]);return length;}}
        public float ProjectArc(Vector3 point,out Vector3 projected){float best=float.MaxValue,arc=0,prior=0;projected=transform.position;if(!IsLine)return 0;for(int i=1;i<linePoints.Length;i++){Vector3 a=linePoints[i-1],d=linePoints[i]-a;float length=d.magnitude,t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0;Vector3 p=a+t*d;float distance=Vector2.Distance(point,p);if(distance<best){best=distance;projected=p;arc=prior+t*length;}prior+=length;}return arc;}
        public Vector3 PointAtArc(float arc){if(!IsLine)return transform.position;for(int i=1;i<linePoints.Length;i++){float length=Vector2.Distance(linePoints[i-1],linePoints[i]);if(arc<=length)return length>0?Vector3.Lerp(linePoints[i-1],linePoints[i],Mathf.Clamp01(arc/length)):linePoints[i];arc-=length;}return linePoints[linePoints.Length-1];}
        public Vector3[] Slice(float from,float to){var points=new System.Collections.Generic.List<Vector3>();from=Mathf.Clamp(from,0,WorldLength);to=Mathf.Clamp(to,from,WorldLength);points.Add(PointAtArc(from));float arc=0;for(int i=1;i<linePoints.Length;i++){arc+=Vector2.Distance(linePoints[i-1],linePoints[i]);if(arc>from+.00001f&&arc<to-.00001f)points.Add(linePoints[i]);}var end=PointAtArc(to);if(Vector3.Distance(points[points.Count-1],end)>.00001f)points.Add(end);return points.ToArray();}
        public void SetFragmentHealth(float current,float maximum){Health=current;MaximumHealth=maximum;Destroyed=false;}
        public bool IsLine {get{return linePoints!=null&&linePoints.Length>=2;}}
        public float DistanceTo(Vector3 point){if(!IsLine)return Vector2.Distance(transform.position,point);float distance=float.MaxValue;for(int i=1;i<linePoints.Length;i++)distance=Mathf.Min(distance,DistanceToSegment(linePoints[i-1],linePoints[i],point));return distance;}
        public Vector3 ClosestPoint(Vector3 point){if(!IsLine)return transform.position;float distance=float.MaxValue;Vector3 closest=linePoints[0];for(int i=1;i<linePoints.Length;i++){Vector3 a=linePoints[i-1],d=linePoints[i]-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0;Vector3 p=a+t*d;float value=Vector2.Distance(p,point);if(value<distance){distance=value;closest=p;}}return closest;}
        public bool VisibleTo(BattleWorld world,string viewer){if(ownerId==viewer||!world.fogEnabled)return true;foreach(var u in world.units)if(u!=null&&u.OnMap&&u.ownerId==viewer&&DistanceTo(u.transform.position)<=u.combatDefinition.visionRadius+radius)return true;return false;}
        public static float DistanceToSegment(Vector3 a,Vector3 b,Vector3 p){Vector2 d=b-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot((Vector2)(p-a),d)/d.sqrMagnitude):0;return Vector2.Distance((Vector2)a+t*d,p);}
        public bool Crosses(Vector3 a,Vector3 b){if(!IsLine)return CampaignRuntime.SegmentHits(a,b,transform.position,radius);for(int i=1;i<linePoints.Length;i++){Vector2 p=linePoints[i-1],q=linePoints[i];if(DistanceToSegment(a,b,p)<=radius||DistanceToSegment(a,b,q)<=radius||DistanceToSegment(p,q,a)<=radius||DistanceToSegment(p,q,b)<=radius||SegmentsCross(a,b,p,q))return true;}return false;}
        public static bool SegmentsCross(Vector2 a,Vector2 b,Vector2 c,Vector2 d){Vector2 u=b-a,v=d-c;float cross=u.x*v.y-u.y*v.x;if(Mathf.Abs(cross)<=Mathf.Epsilon)return false;Vector2 w=c-a;float t=(w.x*v.y-w.y*v.x)/cross,s=(w.x*u.y-w.y*u.x)/cross;return t>=0&&t<=1&&s>=0&&s<=1;}
        public static bool SegmentsOverlap(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {if(SegmentsCross(a,b,c,d))return true;Vector2 u=b-a;if(u.sqrMagnitude<=Mathf.Epsilon)return false;float cross=u.x*(c.y-a.y)-u.y*(c.x-a.x);float parallel=u.x*(d.y-c.y)-u.y*(d.x-c.x);if(Mathf.Abs(cross)>.00001f||Mathf.Abs(parallel)>.00001f)return false;float t0=Vector2.Dot(c-a,u)/u.sqrMagnitude,t1=Vector2.Dot(d-a,u)/u.sqrMagnitude;return Mathf.Min(1,Mathf.Max(t0,t1))-Mathf.Max(0,Mathf.Min(t0,t1))>.00001f;}
        public void SetLineHealth(float health){Health=MaximumHealth=health;Destroyed=false;}
        public float Health {get;private set;}
        public float MaximumHealth {get;private set;}
        public bool Destroyed {get;private set;}
        public void Initialize(CampaignRules rules){Health=MaximumHealth=rules==null?legacyHealth:rules.fortHealth*Mathf.Pow(rules.fortLevelHealthFactor,level);Destroyed=false;}
        public void Damage(float value){if(Destroyed)return;Health=Mathf.Max(0,Health-Mathf.Max(0,value));if(Health<=0)Demolish();}
        public void Demolish(){Destroyed=true;gameObject.SetActive(false);}
    }
}



