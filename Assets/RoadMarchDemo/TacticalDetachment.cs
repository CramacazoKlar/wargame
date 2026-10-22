using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public enum DetachmentKind { Flank=3, Feint=4, Infiltrate=5 }
    // Runtime identity is private to the owner. The combat definition remains the enemy-facing echelon.
    public sealed class TacticalDetachment : MonoBehaviour
    {
        public string group,baseName;public bool detached;public DetachmentKind kind;
        public float VehicleScale {get{var u=Unit;float original=u.Campaign==null?CombatMath.FullPersonnel(u.combatDefinition.squadSize,u.combatDefinition.echelonRatio,(int)originalEchelon):u.Campaign.rules.At(u.Campaign.rules.troopStrength,originalEchelon);float current=u.Campaign==null?u.combatDefinition.FullPersonnel:u.Campaign.rules.At(u.Campaign.rules.troopStrength,u.combatDefinition.echelon);return share*original/Mathf.Max(.000001f,current);}}
        public static float Scale(BattleUnit u){var d=u.GetComponent<TacticalDetachment>();return d==null?1:d.VehicleScale;}
        public ArmyEchelon originalEchelon;public float originalCapacity,originalAttack,share;
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public string Label(string viewer){if(kind==DetachmentKind.Flank)return baseName+(detached?"·2":"·1")+" "+BattleTextFormatter.Echelon(Unit.combatDefinition.echelon)+(viewer==Unit.ownerId?" "+Mathf.CeilToInt(Unit.Personnel)+"/"+Mathf.CeilToInt(Unit.StrengthCapacity):"");if(viewer!=Unit.ownerId)return baseName;return baseName+(detached?"·分队":"·本队")+" "+Mathf.CeilToInt(Unit.Personnel)+"/"+Mathf.CeilToInt(Unit.StrengthCapacity);}
        public static bool Family(BattleUnit a,BattleUnit b){var x=a==null?null:a.GetComponent<TacticalDetachment>();var y=b==null?null:b.GetComponent<TacticalDetachment>();return a!=b&&x!=null&&y!=null&&!string.IsNullOrEmpty(x.group)&&x.group==y.group&&a.ownerId==b.ownerId;}
        public static CommandResult Split(BattleWorld w,BattleUnit u,DetachmentKind kind)
        {
            float share,childPeople;var plan=PlanSplit(u,kind,out share,out childPeople);if(!plan.accepted)return plan;
            if(!u.CanMove||!u.gameObject.activeInHierarchy)return CommandResult.No("当前状态不能分兵");
            if(u.GetComponent<TacticalDetachment>()!=null)return CommandResult.No("请先合并本次分兵，再重新拆分");
            var carrier=u.GetComponent<PersonnelCarrier>();var truck=u.GetComponent<TransportTruck>();
            if(carrier!=null&&carrier.Passengers.Count>0||truck!=null&&truck.Cargo>0)return CommandResult.No("请先卸下乘员或物资，再分兵");
            var free=u.GetComponent<ArmyFreeMarchMover>();if(free==null)return CommandResult.No("部队缺少移动配置");
            Vector3 spawn=u.transform.position;bool found=false;float distance=Mathf.Max(.3f,w.setup.mapCamera.orthographicSize*70/Mathf.Max(1,w.setup.mapCamera.pixelHeight));
            for(int ring=1;ring<=3&&!found;ring++)for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;var p=u.transform.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*distance*ring;if(!free.CanTraverse(w.setup.freeMarchMap,new RoadPoint(u.transform.position.x,u.transform.position.y),new RoadPoint(p.x,p.y)))continue;bool occupied=false;foreach(var other in w.units)if(other!=null&&other!=u&&other.OnMap&&Vector2.Distance(p,other.transform.position)<distance*.65f){occupied=true;break;}if(occupied)continue;spawn=p;found=true;break;}
            if(!found)return CommandResult.No("周围没有足够的可通行位置");
            float capacity=u.Capacity,people=u.Personnel;
            if(childPeople<=0||people-childPeople<=0)return CommandResult.No("现有兵力无法拆成两支存活部队");
            string name=u.DisplayDesignation,id=Guid.NewGuid().ToString("N");var echelon=u.combatDefinition.echelon;float attack=u.combatDefinition.fullStrengthAttack;
            var obj=UnityEngine.Object.Instantiate(u.gameObject,u.transform.parent);obj.name=u.name+" detachment";
            foreach(var route in obj.GetComponentsInChildren<RoadRouteView>()){route.gameObject.SetActive(false);UnityEngine.Object.Destroy(route.gameObject);}
            var child=obj.GetComponent<BattleUnit>();child.unitId=Guid.NewGuid().ToString("N");child.ownerId=u.ownerId;child.combatDefinition=UnityEngine.Object.Instantiate(u.combatDefinition);child.NetworkDefinitionOwned=true;
            u.combatDefinition=UnityEngine.Object.Instantiate(u.combatDefinition);u.NetworkDefinitionOwned=true;
            var a=u.gameObject.AddComponent<TacticalDetachment>();var b=obj.AddComponent<TacticalDetachment>();
            foreach(var member in new[]{a,b}){member.group=id;member.baseName=name;member.kind=kind;member.originalCapacity=capacity;member.originalEchelon=echelon;member.originalAttack=attack;}
            a.share=1-share;b.share=share;b.detached=true;
            child.Campaign=u.Campaign;Configure(u,a,capacity*(1-share));Configure(child,b,capacity*share);
            var mover=obj.GetComponent<ArmyRoadMover>();mover.waterMap=w.setup.freeMarchMap.waterMap;mover.terrainMap=w.setup.freeMarchMap;
            mover.InitializeAt(w.setup.roadMap.Network,new RoadPoint(spawn.x,spawn.y),RoadRouteView.Create(obj.transform,obj.name+" route",.06f,3,StaffMapStyle.Faction(u.ownerId==BattleFactions.BluePlayerId)));
            var childFree=obj.GetComponent<ArmyFreeMarchMover>();childFree.Initialize(mover.Motor);childFree.BindMap(w.setup.freeMarchMap);
            if(w.campaign!=null)w.campaign.ConfigureUnit(child);child.Initialize();child.SetPersonnelExact(childPeople);u.SetPersonnelExact(people-childPeople);u.Motor.Stop();
            var oldState=u.GetComponent<CampaignUnit>();var newState=child.GetComponent<CampaignUnit>();if(oldState!=null&&newState!=null)newState.AttackRemaining=oldState.AttackRemaining;
            var gun=u.GetComponent<ArtilleryBattery>();if(gun!=null)child.GetComponent<ArtilleryBattery>().SetSplitCooldown(gun.Remaining);if(truck!=null){truck.StopAutomatic();child.GetComponent<TransportTruck>().StopAutomatic();}
            UnitExperience.OnSplit(u,child);child.View.Initialize();w.RegisterUnit(child,mover);if(w.currentPlayer==u.ownerId)w.setup.modes.SetMode(MouseCommandMode.FreeMarch);
            if(w.currentPlayer==u.ownerId)UnityEngine.Object.FindObjectOfType<CampaignHUD>()?.SelectUnit(child);
            return CommandResult.Yes();
        }
        public static CommandResult PlanSplit(BattleUnit u,DetachmentKind kind,out float share,out float childPeople)
        {
            share=childPeople=0;if(u==null||!u.CanMove||!u.gameObject.activeInHierarchy)return CommandResult.No("当前状态不能分兵");
            if(!Enum.IsDefined(typeof(DetachmentKind),kind))return CommandResult.No("分兵类型无效");
            if(u.GetComponent<TacticalDetachment>()!=null)return CommandResult.No("请先合并本次分兵，再重新拆分");
            var carrier=u.GetComponent<PersonnelCarrier>();var truck=u.GetComponent<TransportTruck>();
            if(carrier!=null&&carrier.Passengers.Count>0||truck!=null&&truck.Cargo>0)return CommandResult.No("请先卸下乘员或物资，再分兵");
            if(u.GetComponent<ArmyFreeMarchMover>()==null)return CommandResult.No("部队缺少移动配置");
            share=kind==DetachmentKind.Flank?.5f:kind==DetachmentKind.Feint?2f/3:1f/3;
            childPeople=kind==DetachmentKind.Flank?u.Personnel*.5f:Mathf.Min(u.Personnel,u.Capacity*share);
            if(kind!=DetachmentKind.Flank&&u.Personnel<=u.Capacity*share){share=1f/3;childPeople=u.Personnel/3;}
            return childPeople>0&&u.Personnel-childPeople>0?CommandResult.Yes():CommandResult.No("现有兵力无法拆成两支存活部队");
        }
        static void Configure(BattleUnit u,TacticalDetachment d,float capacity)
        {
            u.CapacityOverride=capacity;u.combatDefinition.fullStrengthAttack=d.originalAttack*d.share;
            if(d.kind==DetachmentKind.Flank){var e=d.originalEchelon;while(e>ArmyEchelon.Squad&&u.Campaign!=null&&u.Campaign.rules.At(u.Campaign.rules.troopStrength,e)*u.combatDefinition.squadHealth/Mathf.Max(1,u.Campaign.rules.At(u.Campaign.rules.troopStrength,ArmyEchelon.Squad))>capacity+.00001f)e--;u.combatDefinition.echelon=e;}
        }
        public static CommandResult Merge(BattleWorld w,BattleUnit actor,BattleUnit target)
        {
            if(!Family(actor,target)||!actor.CanMove||!target.CanMove||!target.gameObject.activeInHierarchy)return CommandResult.No("只能合并同一次分兵的两支己方部队");
            if(Vector2.Distance(actor.transform.position,target.transform.position)>UnitRanges.Interaction(actor))return CommandResult.No("尚未靠近分兵部队");
            foreach(var u in new[]{actor,target}){var c=u.GetComponent<PersonnelCarrier>();var t=u.GetComponent<TransportTruck>();if(c!=null&&c.Passengers.Count>0||t!=null&&t.Cargo>0)return CommandResult.No("请先卸下乘员或物资，再合并");}
            UnitExperience.OnMerge(actor,target);var data=target.GetComponent<TacticalDetachment>();float people=actor.Personnel+target.Personnel;
            target.CapacityOverride=data.originalCapacity;target.combatDefinition.echelon=data.originalEchelon;target.combatDefinition.fullStrengthAttack=data.originalAttack;target.SetPersonnelExact(people);target.Motor.Stop();
            var x=actor.GetComponent<CampaignUnit>();var y=target.GetComponent<CampaignUnit>();if(x!=null&&y!=null)y.AttackRemaining=Mathf.Max(x.AttackRemaining,y.AttackRemaining);
            var gunA=actor.GetComponent<ArtilleryBattery>();var gunB=target.GetComponent<ArtilleryBattery>();if(gunA!=null&&gunB!=null)gunB.SetSplitCooldown(Mathf.Max(gunA.Remaining,gunB.Remaining));
            var style=w.GetComponent<StaffMapPresentation>();if(style!=null)style.SetDesignation(target,data.baseName);UnityEngine.Object.Destroy(data);
            actor.Motor.Stop();actor.SetPersonnelExact(0);actor.gameObject.SetActive(false);var units=new List<BattleUnit>(w.units);units.Remove(actor);w.units=units.ToArray();var armies=new List<ArmyRoadMover>(w.setup.sceneArmies);armies.Remove(actor.GetComponent<ArmyRoadMover>());w.setup.sceneArmies=armies.ToArray();UnityEngine.Object.Destroy(actor.gameObject);
            if(target.ownerId==w.currentPlayer)UnityEngine.Object.FindObjectOfType<CampaignHUD>()?.SelectUnit(target);return CommandResult.Yes();
        }
    }
}
