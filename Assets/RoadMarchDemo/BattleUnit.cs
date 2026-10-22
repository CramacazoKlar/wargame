using UnityEngine;
namespace CampaignDemo
{
    public enum ArmyBattleState { Deployed, Engaged, Escaping, EnteringPoint, InsidePoint, Repairing, ExitingPoint, Eliminated }
    public sealed class BattleUnit : MonoBehaviour
    {
        [System.NonSerialized] public CampaignRuntime Campaign;
        public string unitId;
        [Tooltip("模式3显示的部队番号；留空使用物体名称，不向敌方公开。")]
        public string designation;
        public string DisplayDesignation { get { var style=Campaign==null||Campaign.world==null?null:Campaign.world.GetComponent<StaffMapPresentation>();return style!=null?style.Designation(this):string.IsNullOrWhiteSpace(designation)?name:designation; } }
        public string ownerId="A";
        public ArmyCombatDefinition combatDefinition;
        [Range(1,100)] public float startingPersonnelPercent=100;
        public PersonnelCarrier Carrier {get;private set;}
        public bool Working {get;private set;}
        public bool UnderFire {get;set;}
        public void SetCarrier(PersonnelCarrier carrier){Carrier=carrier;if(carrier==null&&Alive)State=ArmyBattleState.Deployed;ApplyMovement();}
        public void SetWorking(bool value){Working=value;ApplyMovement();}
        public bool HiddenInForest {get;private set;}
        public bool ForestAttackEnabled {get;private set;}
        public float Personnel { get; private set; }
        [System.NonSerialized] public float CapacityOverride;
        [System.NonSerialized] public float NetworkStrengthCapacity;
        public float StrengthCapacity {get{if(NetworkStrengthCapacity>0)return NetworkStrengthCapacity;var d=GetComponent<TacticalDetachment>();return d!=null&&d.kind!=DetachmentKind.Flank?d.originalCapacity:Capacity;}}
        public float StrengthFraction {get{return StrengthCapacity<=0?0:Mathf.Clamp01(Personnel/StrengthCapacity);}}
        public void SetPersonnelExact(float value){Personnel=Mathf.Clamp(value,0,Capacity);ApplyMovement();}
        public float Capacity { get { if(CapacityOverride>0)return CapacityOverride; if(Campaign!=null)return Campaign.CapacityFor(this);return GetComponent<PersonnelCarrier>()!=null?GetComponent<PersonnelCarrier>().maxHealth:combatDefinition==null?0:combatDefinition.FullPersonnel; } }
        public float Fraction { get { return Capacity<=0?0:Personnel/Capacity; } }
        public ArmyBattleState State { get; private set; }
        public RecoveryPoint Point { get; private set; }
        public float StateTimer { get; private set; }
        public bool Alive { get { return Personnel>0&&State!=ArmyBattleState.Eliminated; } }
        public bool OnMap { get { return Alive&&Carrier==null&&State!=ArmyBattleState.InsidePoint&&State!=ArmyBattleState.Repairing&&State!=ArmyBattleState.ExitingPoint; } }
        public bool CanMove { get { return GetComponent<RadarInstallation>()==null&&(Campaign==null||!Campaign.world.SimulationEnded)&&OnMap&&!Working&&(State==ArmyBattleState.Deployed||State==ArmyBattleState.Engaged||State==ArmyBattleState.Escaping); } }
        public bool CanFire { get { return GetComponent<RadarInstallation>()==null&&(Campaign==null||!Campaign.world.SimulationEnded)&&OnMap&&!Working&&combatDefinition!=null&&!ArtilleryBattery.IsArtilleryRole(combatDefinition.role)&&GetComponent<PersonnelCarrier>()==null&&(!HiddenInForest||ForestAttackEnabled)&&(State==ArmyBattleState.Deployed||State==ArmyBattleState.Engaged); } }
        ArmyRouteMotor cachedMotor;BattleUnitView cachedView;public BattleUnitView View {get{if(cachedView==null)cachedView=GetComponent<BattleUnitView>();return cachedView;}}
        public ArmyRouteMotor Motor { get { if(cachedMotor==null)cachedMotor=GetComponent<ArmyRouteMotor>();return cachedMotor; } }
        public void Initialize()
        { PlannedUnitRanges.ApplyTo(this);if(combatDefinition==null)throw new System.ArgumentException("Missing combat definition: "+name);GetComponent<CampaignUnit>()?.ResetRetreatEncounter();var combatState=GetComponent<CampaignUnit>();if(combatState!=null)combatState.AttackRemaining=0;Personnel=Capacity*Mathf.Clamp(startingPersonnelPercent,1,100)/100;State=ArmyBattleState.Deployed;Point=null;Carrier=null;Working=false;StateTimer=0;RevealForest();ApplyMovement(); }
        [System.NonSerialized] public bool NetworkVisible=true,NetworkDefinitionOwned;
        public void ApplyNetworkState(PvpUnit data,RecoveryPoint city){transform.position=data.position;if(data.state==ArmyBattleState.Eliminated&&Alive)Campaign?.world.Replay?.RecordDeath(this);NetworkStrengthCapacity=data.strengthCapacity;CapacityOverride=data.capacity;Personnel=data.personnel;State=data.state;Point=city;Working=data.working;HiddenInForest=data.hidden;ForestAttackEnabled=data.ambush;StateTimer=data.timer;UnderFire=data.underFire;ApplyMovement();}
        public void ApplyNetworkCarrier(PersonnelCarrier carrier){Carrier=carrier;ApplyMovement();}
        void OnDestroy(){if(NetworkDefinitionOwned&&combatDefinition!=null)Destroy(combatDefinition);}
        public void Damage(float amount)
        { if(!Alive||(Campaign!=null&&Campaign.world.SimulationEnded))return;if(amount>0)Campaign?.world.Replay?.BeginContact();float before=Personnel;if(amount>0){RevealForest();UnderFire=true;if(Campaign!=null&&Campaign.lineWorks!=null&&Campaign.lineWorks.Working(this))Campaign.lineWorks.Cancel(this);if(Campaign!=null&&Working)SetWorking(false);}Personnel=Mathf.Max(0,Personnel-Mathf.Max(0,amount));var truck=GetComponent<TransportTruck>();if(truck!=null)truck.ApplyPersonnelLoss(before,Personnel);var personnelCarrier=GetComponent<PersonnelCarrier>();if(personnelCarrier!=null&&Campaign!=null)personnelCarrier.ApplyPassengerLoss(before,Personnel);if(Personnel<=0)Eliminate(); }
        public void Restore(float amount) { if(Alive)Personnel=Mathf.Min(Capacity,Personnel+Mathf.Max(0,amount)); }
        public void Engage()
        { if(!OnMap||State==ArmyBattleState.Escaping)return;Point=null;State=ArmyBattleState.Engaged;StateTimer=0;var m=Motor;ApplyMovement(); }
        public void ReleaseCombat() { if(State==ArmyBattleState.Engaged){State=ArmyBattleState.Deployed;ApplyMovement();} }
        public bool Escape(float lossFraction,float grace)
        { if(Campaign!=null)return false;if(State!=ArmyBattleState.Engaged||!Alive)return false;Damage(Personnel*Mathf.Clamp01(lossFraction));if(!Alive)return true;State=ArmyBattleState.Escaping;StateTimer=Mathf.Max(.1f,grace);var m=Motor;if(m!=null)m.Stop();ApplyMovement();return true; }
        public bool BeginEnter(RecoveryPoint point)
        { if(!CanMove||point==null)return false;RevealForest();Point=point;State=ArmyBattleState.InsidePoint;StateTimer=0;var m=Motor;if(m!=null)m.Stop();ApplyMovement();if(point.isRepairSite)FacilityRecoveryQueue.For(this)?.Enqueue(this);return true; }
        public void CompleteEnter() { State=ArmyBattleState.InsidePoint;StateTimer=0;ApplyMovement(); }
        public bool BeginRepair() { if(State!=ArmyBattleState.InsidePoint||Point==null||Fraction>=1)return false;State=ArmyBattleState.Repairing;ApplyMovement();return true; }
        public void CancelRepair() { if(State==ArmyBattleState.Repairing){State=ArmyBattleState.InsidePoint;ApplyMovement();} }
        public bool BeginExit()
        { if(Point==null||(State!=ArmyBattleState.InsidePoint&&State!=ArmyBattleState.Repairing))return false;FacilityRecoveryQueue.For(this)?.Cancel(this);StateTimer=0;CompleteExit();return true; }
        public void AdvanceTimer(float dt) { StateTimer=Mathf.Max(0,StateTimer-dt);if(State==ArmyBattleState.Escaping&&StateTimer<=0){State=ArmyBattleState.Deployed;ApplyMovement();} }
        public void CompleteExit()
        { if(Point!=null)transform.position=Point.ExitPositionFor(this);Point=null;State=ArmyBattleState.Deployed;StateTimer=0;ApplyMovement(); }
        public void Eliminate() { if(Campaign!=null&&Campaign.world.SimulationEnded)return;Campaign?.world.Replay?.RecordDeath(this);var carrier=GetComponent<PersonnelCarrier>();if(carrier!=null)carrier.KillPassengers();Carrier=null;Working=false;RevealForest(); var truck=GetComponent<TransportTruck>();if(truck!=null)truck.ApplyPersonnelLoss(Personnel,0);Personnel=0;State=ArmyBattleState.Eliminated;Point=null;var m=Motor;if(m!=null)m.Stop();ApplyMovement(); }
        public void RevealForest(){HiddenInForest=false;ForestAttackEnabled=false;}
        public bool HideInForest(){if(GetComponent<RadarInstallation>()!=null||!combatDefinition.canHideInForest||State!=ArmyBattleState.Deployed||!OnMap||Working||(Campaign!=null&&UnderFire))return false;var motor=Motor;if(motor!=null)motor.Stop();var truck=GetComponent<TransportTruck>();if(truck!=null)truck.StopAutomatic();HiddenInForest=true;ForestAttackEnabled=false;return true;}
        public bool SetForestAttack(bool enabled){if(!HiddenInForest)return false;ForestAttackEnabled=enabled;return true;}
        void ApplyMovement() { var m=Motor;if(m!=null)m.MovementLocked=!CanMove; }
    }
}








