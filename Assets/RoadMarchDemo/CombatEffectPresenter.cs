using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Local view only. No simulation depends on this component being present or enabled.
    public sealed class CombatEffectPresenter : MonoBehaviour
    {
        public BattleWorld world;
        [Tooltip("实现ICombatEffectPlayer的组件。可用PrefabCombatEffectPlayer或自行替换。")] public MonoBehaviour effectPlayer;
        ICombatEffectPlayer Player {get{return effectPlayer as ICombatEffectPlayer;}}
        BattleWorld subscribedWorld;
        readonly Dictionary<string,double> nextDirect=new Dictionary<string,double>();
        public CombatEffectProfile defaultProfile;
        [Serializable] public sealed class RoleProfile {public UnitRole role;public CombatEffectProfile profile;}
        public RoleProfile[] profiles=new RoleProfile[0];
        public CombatEffectProfile artilleryProfile,supportProfile;
        public CombatEffectProfile Profile(CombatEffectCue cue)
        {
            if(cue.Stage==CombatEffectStage.SupportLaunch||cue.Stage==CombatEffectStage.SupportImpact)return supportProfile==null?defaultProfile:supportProfile;
            if(cue.Stage==CombatEffectStage.ArtilleryLaunch||cue.Stage==CombatEffectStage.ArtilleryImpact)return artilleryProfile==null?defaultProfile:artilleryProfile;
            if(cue.HasSourceRole&&profiles!=null)foreach(var p in profiles)if(p!=null&&p.role==cue.SourceRole&&p.profile!=null)return p.profile;
            return defaultProfile;
        }
        void OnEnable(){Bind();}
        void Update(){if(subscribedWorld!=world)Bind();}
        public void BindWorld(BattleWorld target){world=target;Bind();}
        void Bind(){Reset();Unbind();if(world==null)world=GetComponent<BattleWorld>();if(world==null)return;subscribedWorld=world;world.CombatEffectOccurred+=OnEffect;world.PlayerChanged+=Reset;}
        void Unbind(){if(subscribedWorld!=null){subscribedWorld.CombatEffectOccurred-=OnEffect;subscribedWorld.PlayerChanged-=Reset;}subscribedWorld=null;}
        void OnDisable(){Unbind();Reset();}
        void OnDestroy(){Unbind();}
        public void Reset(){nextDirect.Clear();try{if(Player!=null)Player.Clear();}catch(Exception e){Debug.LogException(e,this);}}
        BattleUnit Find(string id){if(world.units==null||string.IsNullOrEmpty(id))return null;foreach(var u in world.units)if(u!=null&&u.unitId==id)return u;return null;}
        public bool TryCreateCue(CombatEffectEvent e,out CombatEffectCue cue)
        {
            cue=default;if(world==null)return false;string viewer=world.currentPlayer;
            var source=Find(e.SourceId);bool sourceVisible=e.HasSource&&(e.SourceOwner==viewer||(!world.fogEnabled)||(source!=null&&world.IsVisible(viewer,source)));
            var target=Find(e.TargetId);bool targetVisible=e.TargetOwner==viewer||!world.fogEnabled||(target!=null?world.IsVisible(viewer,target):world.VisibleAt(viewer,e.TargetPosition));
            bool launch=e.Stage==CombatEffectStage.ArtilleryLaunch||e.Stage==CombatEffectStage.SupportLaunch;
            if(e.Stage==CombatEffectStage.SupportLaunch){if(e.SourceOwner!=viewer)return false;targetVisible=true;}
            else if(launch){if(!sourceVisible)return false;targetVisible=e.SourceOwner==viewer||targetVisible;}
            else if(e.Stage==CombatEffectStage.ArtilleryImpact||e.Stage==CombatEffectStage.SupportImpact){sourceVisible=false;if(!targetVisible)return false;}
            else if(!sourceVisible&&!targetVisible)return false;
            bool knowsRole=sourceVisible&&(e.SourceOwner==viewer||(source!=null&&world.IsKnown(viewer,source)));
            cue=new CombatEffectCue(e.Stage,sourceVisible,targetVisible,knowsRole,knowsRole?e.SourceRole:UnitRole.Infantry,e.SourcePosition,e.TargetPosition,e.Radius);return true;
        }
        public void Present(CombatEffectEvent e,double displayTime)
        {
            if(!isActiveAndEnabled||Player==null||!TryCreateCue(e,out var cue))return;
            if(e.Stage==CombatEffectStage.DirectAttack){string key=e.SourceId??"unknown";if(nextDirect.TryGetValue(key,out var next)&&displayTime<next)return;var profile=Profile(cue);nextDirect[key]=displayTime+(profile==null?.35:Mathf.Max(0,profile.directAttackInterval));}
            Player.Play(cue);
        }
        void OnEffect(CombatEffectEvent e){Present(e,Time.timeAsDouble);}
    }
}
