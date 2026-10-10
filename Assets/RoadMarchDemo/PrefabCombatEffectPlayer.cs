using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Default replaceable renderer. Prefabs may contain particles, sprites or animations.
    public sealed class PrefabCombatEffectPlayer : MonoBehaviour,ICombatEffectPlayer
    {
        public CombatEffectPresenter presenter;
        [Min(1)] public int maxActiveObjects=128;
        readonly List<GameObject> active=new List<GameObject>();
        Material material;
        public void Play(CombatEffectCue cue)
        {
            if(!Application.isPlaying||!isActiveAndEnabled||presenter==null)return;var profile=presenter.Profile(cue);if(profile==null)return;active.RemoveAll(g=>g==null);
            bool direct=cue.Stage==CombatEffectStage.DirectAttack;bool launch=cue.Stage==CombatEffectStage.ArtilleryLaunch;
            bool impact=direct||cue.Stage==CombatEffectStage.ArtilleryImpact||cue.Stage==CombatEffectStage.SupportImpact;
            if((direct||launch)&&cue.HasSource)Spawn(profile.firePrefab,cue.SourcePosition,profile.lifetime,profile.prefabScale);
            if((direct||launch)&&cue.HasSource&&cue.HasTarget&&profile.projectilePrefab!=null){var g=Spawn(profile.projectilePrefab,cue.SourcePosition,Mathf.Max(profile.lifetime,profile.projectileTravelSeconds),profile.prefabScale);if(g!=null)g.GetComponent<CombatEffectInstance>().Configure(Mathf.Max(profile.lifetime,profile.projectileTravelSeconds),cue.SourcePosition,cue.TargetPosition,profile.projectileTravelSeconds);}
            if(impact&&cue.HasTarget){float scale=profile.prefabScale*(profile.scaleImpactByBlastRadius&&cue.Radius>0?cue.Radius:1);Spawn(profile.impactPrefab,cue.TargetPosition,profile.lifetime,scale);}
            if(!profile.showPlaceholder)return;
            if(direct&&cue.HasSource&&cue.HasTarget&&profile.projectilePrefab==null)Line(cue.SourcePosition,cue.TargetPosition,profile);
            if(impact&&cue.HasTarget&&profile.impactPrefab==null)Ring(cue.TargetPosition,cue.Radius>0?cue.Radius:profile.directImpactRadius,profile);
            if(launch&&cue.HasSource&&profile.firePrefab==null)Ring(cue.SourcePosition,profile.directImpactRadius,profile);
        }
        GameObject Track(GameObject g)
        {active.RemoveAll(item=>item==null);while(active.Count>=Mathf.Max(1,maxActiveObjects)){var first=active[0];active.RemoveAt(0);Release(first);}active.Add(g);return g;}
        GameObject Spawn(GameObject prefab,Vector3 p,float lifetime,float scale)
        {if(prefab==null)return null;var g=Instantiate(prefab,p,Quaternion.identity);g.transform.localScale*=Mathf.Max(.01f,scale);var timer=g.GetComponent<CombatEffectInstance>();if(timer==null)timer=g.AddComponent<CombatEffectInstance>();timer.Configure(lifetime,p,p);return Track(g);}
        LineRenderer CreateLine(CombatEffectProfile profile)
        {if(material==null){var shader=Shader.Find("Sprites/Default");if(shader==null)return null;material=new Material(shader);}var g=Track(new GameObject("攻击特效占位"));var line=g.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=material;line.sortingOrder=25;line.startColor=line.endColor=profile.color;line.startWidth=line.endWidth=profile.lineWidth;g.AddComponent<CombatEffectInstance>().Configure(profile.lifetime,Vector3.zero,Vector3.zero);return line;}
        void Line(Vector3 a,Vector3 b,CombatEffectProfile profile){var line=CreateLine(profile);if(line==null)return;line.positionCount=2;line.SetPositions(new[]{a,b});}
        void Ring(Vector3 p,float radius,CombatEffectProfile profile){var line=CreateLine(profile);if(line==null)return;line.loop=true;line.positionCount=32;for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32;line.SetPosition(i,p+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius));}}
        static void Release(Object obj){if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        public void Clear(){foreach(var g in active)Release(g);active.Clear();}
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();Release(material);}
    }
}

