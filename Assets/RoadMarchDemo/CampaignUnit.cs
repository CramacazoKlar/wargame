using UnityEngine;
using System.Collections.Generic;
namespace CampaignDemo
{
    public sealed class CampaignUnit:MonoBehaviour
    {
        [Min(0)] public float desiredSpeed=100;
        public Fortification fortTarget;
        public int fortBuildLevel;
        readonly Dictionary<BattleUnit,float> recentAttackers=new Dictionary<BattleUnit,float>();
        readonly HashSet<BattleUnit> observedAttackers=new HashSet<BattleUnit>();
        readonly List<Vector3> retreatMotion=new List<Vector3>();
        bool retreatCharged;
        public int RetreatCharges {get;private set;}
        public void BeginCombatObservation(){observedAttackers.Clear();}
        public void ObserveAttack(BattleUnit attacker)
        {if(attacker==null||attacker.ownerId==Unit.ownerId)return;recentAttackers[attacker]=0;observedAttackers.Add(attacker);}
        public void ResetRetreatEncounter(){recentAttackers.Clear();observedAttackers.Clear();retreatMotion.Clear();retreatCharged=false;RetreatCharges=0;}
        public void ResolveRetreatMovement(float dt,CampaignRules rules){bool first=true;foreach(var sample in retreatMotion){ResolveRetreat(first?dt:0,rules,sample.z,new Vector2(sample.x,sample.y));first=false;}if(first)ResolveRetreat(dt,rules,Moving?Unit.Motor.CurrentSpeed:0,HasFacing?Facing:Vector2.zero);retreatMotion.Clear();}
        public float ResolveRetreat(float dt,CampaignRules rules,float actualSpeed,Vector2 direction)
        {
            foreach(var attacker in new List<BattleUnit>(recentAttackers.Keys))
            {
                float age=recentAttackers[attacker]+(observedAttackers.Contains(attacker)?0:Mathf.Max(0,dt));
                if(attacker==null||!attacker.OnMap||age>=rules.retreatEncounterResetSeconds)recentAttackers.Remove(attacker);else recentAttackers[attacker]=age;
            }
            if(recentAttackers.Count==0){retreatCharged=false;return 0;}
            if(retreatCharged||!Unit.OnMap||actualSpeed<=rules.retreatMinimumSpeed||direction.sqrMagnitude<=Mathf.Epsilon)return 0;
            foreach(var attacker in recentAttackers.Keys)
            {
                Vector2 away=(Vector2)(transform.position-attacker.transform.position);
                if(away.sqrMagnitude<=Mathf.Epsilon||Vector2.Dot(direction.normalized,away.normalized)<=rules.retreatAwayDotThreshold)continue;
                retreatCharged=true;RetreatCharges++;float loss=Unit.Personnel*Mathf.Clamp01(rules.retreatLossFraction);Unit.Damage(loss);return loss;
            }
            return 0;
        }
        public Vector2 Facing {get;private set;}
        public bool HasFacing {get;private set;}
        public float Linger {get;private set;}
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public bool Moving {get{return Unit.Motor!=null&&Unit.Motor.IsMoving&&!Unit.Motor.PlanningPaused&&!Unit.Motor.MovementLocked&&Unit.Motor.CurrentSpeed>0;}}
        public void RecordMovement(Vector3 delta)
        {if(delta.sqrMagnitude<=Mathf.Epsilon)return;retreatMotion.Add(new Vector3(delta.x,delta.y,Unit.Motor==null?0:Unit.Motor.CurrentSpeed));Facing=((Vector2)delta).normalized;HasFacing=true;Linger=Unit.Campaign==null?0:Unit.Campaign.rules.infantryFacingLingerSeconds;}
        public void Advance(float dt)
        {if(Moving)return;Linger=Mathf.Max(0,Linger-dt);if(Unit.combatDefinition.role!=UnitRole.Tank&&Linger<=0)HasFacing=false;}
        public float FlankFrom(Vector3 attacker,CampaignRules r)
        {
            if(!HasFacing)return 1;float angle=Vector2.Angle(Facing,(Vector2)attacker-(Vector2)transform.position);bool tank=Unit.combatDefinition.role==UnitRole.Tank;
            if(angle<=r.frontHalfAngle)return 1;
            if(angle>=r.rearStartsAngle)return tank?r.tankRearMultiplier:r.infantryRearMultiplier;
            return tank?r.tankSideMultiplier:r.infantrySideMultiplier;
        }
    }
}



