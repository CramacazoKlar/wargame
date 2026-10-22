using UnityEngine;
namespace CampaignDemo
{
    public enum CombatEffectStage { DirectAttack, ArtilleryLaunch, ArtilleryImpact }

    // Simulation notification: snapshots only, never references to mutable units or prefabs.
    public readonly struct CombatEffectEvent
    {
        public readonly CombatEffectStage Stage;
        public readonly string SourceId, SourceOwner, TargetId, TargetOwner;
        public readonly UnitRole SourceRole;
        public readonly bool HasSource;
        public readonly Vector3 SourcePosition, TargetPosition;
        public readonly float Radius;
        public CombatEffectEvent(CombatEffectStage stage,string sourceId,string sourceOwner,UnitRole sourceRole,
            bool hasSource,Vector3 sourcePosition,string targetId,string targetOwner,Vector3 targetPosition,
            float radius=0)
        {
            Stage=stage;SourceId=sourceId;SourceOwner=sourceOwner;SourceRole=sourceRole;
            HasSource=hasSource;SourcePosition=sourcePosition;TargetId=targetId;TargetOwner=targetOwner;
            TargetPosition=targetPosition;Radius=radius;
        }
        public static CombatEffectEvent Direct(BattleUnit source,string targetId,string targetOwner,Vector3 target)
        {return new CombatEffectEvent(CombatEffectStage.DirectAttack,source.unitId,source.ownerId,source.combatDefinition.role,true,source.transform.position,targetId,targetOwner,target,0);}
    }

    // Presentation input: locations/role that the current viewer cannot know are omitted.
    public readonly struct CombatEffectCue
    {
        public readonly CombatEffectStage Stage;
        public readonly bool HasSource,HasTarget,HasSourceRole;
        public readonly UnitRole SourceRole;
        public readonly Vector3 SourcePosition,TargetPosition;
        public readonly float Radius;
        public CombatEffectCue(CombatEffectStage stage,bool hasSource,bool hasTarget,bool hasRole,UnitRole role,
            Vector3 source,Vector3 target,float radius)
        {Stage=stage;HasSource=hasSource;HasTarget=hasTarget;HasSourceRole=hasRole;SourceRole=role;SourcePosition=hasSource?source:Vector3.zero;TargetPosition=hasTarget?target:Vector3.zero;Radius=radius;}
    }

    // Replace this implementation to use another effect system; combat never waits for it.
    public interface ICombatEffectPlayer
    {
        void Play(CombatEffectCue cue);
        void Clear();
    }
}
