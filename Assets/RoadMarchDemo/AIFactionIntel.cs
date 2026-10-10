using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public sealed class AIContactRecord {
  public readonly string id,type;public readonly Vector3 lastObservedPosition;public readonly float observedAt;public readonly bool visible,identified;public readonly ArmyEchelon? echelon;public readonly float? personnelPercent;
  public AIContactRecord(ArmyInfo info,float time,bool visibleNow){id=info.unitId;type=info.unitType;lastObservedPosition=info.position;observedAt=time;visible=visibleNow;identified=info.known;echelon=info.echelon;personnelPercent=info.personnelPercent;}
  public AIContactRecord(AIContactRecord old){id=old.id;type=old.type;lastObservedPosition=old.lastObservedPosition;observedAt=old.observedAt;visible=false;identified=old.identified;echelon=old.echelon;personnelPercent=old.personnelPercent;}
 }
 public interface IAIFactionIntel {
  AIContactRecord[] Contacts {get;}
  ArmyInfo[] OwnUnits();
  FacilityInfo[] OwnCities();
 }
 public sealed class AIFactionIntel:MonoBehaviour,IAIFactionIntel {
  public BattleAIController controller;
  readonly Dictionary<string,AIContactRecord> contacts=new Dictionary<string,AIContactRecord>();
  public AIContactRecord[] Contacts {get{var list=new List<AIContactRecord>(contacts.Values);list.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));return list.ToArray();}}
  public void Observe(float now){if(controller==null||controller.information==null)return;var visible=new HashSet<string>();foreach(var id in controller.information.GetVisibleArmyIds(controller.playerId)){var info=controller.information.GetArmy(controller.playerId,id);if(info==null||info.own)continue;visible.Add(id);contacts[id]=new AIContactRecord(info,now,true);}foreach(var id in new List<string>(contacts.Keys))if(!visible.Contains(id)){var profile=controller.profile;if(profile==null||!profile.enemyMemory||now-contacts[id].observedAt>=profile.enemyMemorySeconds)contacts.Remove(id);else contacts[id]=new AIContactRecord(contacts[id]);}}
  public ArmyInfo[] OwnUnits(){var list=new List<ArmyInfo>();if(controller==null||controller.information==null)return list.ToArray();foreach(var id in controller.information.GetVisibleArmyIds(controller.playerId,true)){var info=controller.information.GetArmy(controller.playerId,id);if(info!=null&&info.own)list.Add(info);}return list.ToArray();}
  public FacilityInfo[] OwnCities(){var list=new List<FacilityInfo>();if(controller==null||controller.information==null)return list.ToArray();foreach(var id in controller.information.GetFacilityIds(controller.playerId)){var info=controller.information.GetFacility(controller.playerId,id);if(info!=null&&info.own)list.Add(info);}return list.ToArray();}
 }
}
