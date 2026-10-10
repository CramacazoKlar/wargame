using UnityEngine;
using UnityEngine.Events;
namespace CampaignDemo {
 public sealed class AICityReliefTrigger:MonoBehaviour {
  public BattleAIController controller;
  public RecoveryPoint city;
  public string[] taskKeys=new string[0];
  [Min(0)] public float threatRadius=3,requiredThreatSeconds=3;
  public UnityEvent onReliefCalled=new UnityEvent();
  [SerializeField] bool triggered;
  [SerializeField] float threatSeconds;
  public bool Triggered {get{return triggered;}}
  public void ResetTrigger(){triggered=false;threatSeconds=0;}
  void Update(){Tick(Time.deltaTime);}
  public void Tick(float dt){
   if(city!=null&&controller!=null&&city.ownerId!=controller.playerId){threatSeconds=0;return;}
   if(controller==null||controller.profile==null||!controller.profile.cityRelief)return;
   if(triggered||taskKeys==null||taskKeys.Length==0||controller==null||!controller.aiEnabled||controller.world==null||!controller.world.Initialized||city==null||city.ownerId!=controller.playerId||(controller.pauseWhenPlayerControlled&&controller.world.currentPlayer==controller.playerId))return;
   controller.Refresh();if(controller.information==null)return;bool threatened=false;
   foreach(var id in controller.information.GetVisibleArmyIds(controller.playerId)){var info=controller.information.GetArmy(controller.playerId,id);if(info!=null&&!info.own&&info.visible&&info.alive&&info.onMap&&Vector2.Distance(info.position,city.transform.position)<=threatRadius){threatened=true;break;}}
   if(!threatened){threatSeconds=0;return;}threatSeconds+=Mathf.Max(0,dt);if(threatSeconds<requiredThreatSeconds)return;
   int count=0;foreach(var key in taskKeys)count+=controller.ActivateTask(key);if(count==0)return;triggered=true;onReliefCalled.Invoke();
  }
 }
}



