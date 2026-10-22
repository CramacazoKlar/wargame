using UnityEngine;
namespace CampaignDemo
{
    [RequireComponent(typeof(BattleUnit))]
    public sealed class ArtilleryBattery : MonoBehaviour
    {
        public ArtilleryDefinition definition;
        public static bool IsArtilleryRole(UnitRole role){return role==UnitRole.Artillery||role==UnitRole.RocketArtillery;}
        public float Remaining {get;private set;}
        public float BarrageRemaining {get;private set;}
        public float BarrageDuration {get{return definition==null||Unit.combatDefinition==null?0:definition.BarrageDuration(Unit.combatDefinition.role,Unit.combatDefinition.echelon);}}
        public bool Firing {get{return BarrageRemaining>0;}}
        bool networkDefinitionOwned;
        void OnDestroy(){if(networkDefinitionOwned&&definition!=null)Destroy(definition);}
        Vector3 firingOrigin;
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public bool Ready {get{return definition!=null&&(Unit.Motor==null||(!Unit.Motor.IsMoving&&Mathf.Abs(Unit.Motor.CurrentSpeed)<=.000001f))&&Unit.OnMap&&ArtilleryBattery.IsArtilleryRole(Unit.combatDefinition.role)&&(Unit.State==ArmyBattleState.Deployed||Unit.State==ArmyBattleState.Engaged)&&Remaining<=0&&!Firing&&!Unit.Working;}}
        public CommandResult Fire(BattleWorld world,string player,Vector3 target)
        {
            if(world==null||player!=Unit.ownerId||!Ready)return CommandResult.No("炮兵尚未就绪或当前状态不能发射");
            if(!CombinedArmsWorld.Finite(target))return CommandResult.No("目标位置无效");
            target.z=0;
            if(Vector2.Distance(transform.position,target)>definition.range)return CommandResult.No("目标超出射程");
            var system=world.GetComponent<CombinedArmsWorld>();if(system==null)return CommandResult.No("炮击系统未配置");
            BarrageRemaining=BarrageDuration;firingOrigin=transform.position;
            Remaining=Mathf.Max(BarrageRemaining,Mathf.Max(0,definition.cooldownSeconds));
            if(Unit.Motor!=null)Unit.Motor.Stop();Unit.RevealForest();
            system.Schedule(player,target,definition,Unit.Campaign==null?CombatMath.Strength(Unit.Fraction):Unit.Fraction,Unit);
            return CommandResult.Yes();
        }
        public void SetSplitCooldown(float value){Remaining=Mathf.Max(0,value);}
        public void ApplyNetworkState(PvpUnit data){if(!string.IsNullOrEmpty(data.artillery)){if(!networkDefinitionOwned){definition=definition==null?ScriptableObject.CreateInstance<ArtilleryDefinition>():Instantiate(definition);networkDefinitionOwned=true;}JsonUtility.FromJsonOverwrite(data.artillery,definition);}Remaining=data.artilleryCooldown;BarrageRemaining=data.artilleryBarrageRemaining;}
        public void Tick(float dt){dt=Mathf.Max(0,dt);Remaining=Mathf.Max(0,Remaining-dt);BarrageRemaining=Mathf.Max(0,BarrageRemaining-dt);if(!Unit.OnMap||Unit.Working||Vector2.Distance(transform.position,firingOrigin)>.01f||(Unit.Motor!=null&&Unit.Motor.IsMoving))BarrageRemaining=0;}
    }
}
