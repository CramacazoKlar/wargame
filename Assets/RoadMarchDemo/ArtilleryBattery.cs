using UnityEngine;
namespace CampaignDemo
{
    [RequireComponent(typeof(BattleUnit))]
    public sealed class ArtilleryBattery : MonoBehaviour
    {
        public ArtilleryDefinition definition;
        public bool Deployed {get;private set;}
        public float DeploymentRemaining {get;private set;}
        bool packing;
        public CommandResult ToggleDeployment(){if(Unit.Campaign==null)return CommandResult.No("未绑定战役系统");if(DeploymentRemaining>0){DeploymentRemaining=0;Unit.SetWorking(false);return CommandResult.Yes();}if(!Unit.OnMap)return CommandResult.No("炮兵不在地图");if(Unit.Motor!=null)Unit.Motor.Stop();packing=Deployed;DeploymentRemaining=packing?Unit.Campaign.rules.artilleryPackSeconds:Unit.Campaign.rules.artilleryDeploySeconds;Unit.SetWorking(true);return CommandResult.Yes();}
        public float Remaining {get;private set;}
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public bool Ready {get{return definition!=null&&(Unit.Campaign==null||Deployed)&&Unit.OnMap&&Unit.combatDefinition.role==UnitRole.Artillery&&Unit.State==ArmyBattleState.Deployed&&Remaining<=0&&!Unit.Working;}}
        public CommandResult Fire(BattleWorld world,string player,Vector3 target)
        {
            if(world==null||player!=Unit.ownerId||!Ready)return CommandResult.No("炮兵尚未就绪或当前状态不能发射");
            if(!CombinedArmsWorld.Finite(target))return CommandResult.No("目标位置无效");
            target.z=0;
            if(Vector2.Distance(transform.position,target)>definition.range)return CommandResult.No("目标超出射程");
            if(definition.requireVision&&world.fogEnabled&&!world.VisibleAt(player,target))return CommandResult.No("目标不在己方视野内");
            var system=world.GetComponent<CombinedArmsWorld>();if(system==null)return CommandResult.No("炮击系统未配置");
            Remaining=Mathf.Max(0,definition.cooldownSeconds);
            if(Unit.Motor!=null)Unit.Motor.Stop();Unit.RevealForest();
            system.Schedule(player,target,definition,Unit.Campaign==null?CombatMath.Strength(Unit.Fraction):Unit.Fraction,Unit);
            return CommandResult.Yes();
        }
        public void Tick(float dt){Remaining=Mathf.Max(0,Remaining-dt);if(DeploymentRemaining>0){if(!Unit.OnMap||!Unit.Working){DeploymentRemaining=0;Unit.SetWorking(false);return;}DeploymentRemaining=Mathf.Max(0,DeploymentRemaining-dt);if(DeploymentRemaining<=0){Deployed=!packing;Unit.SetWorking(false);}}}
    }
}




