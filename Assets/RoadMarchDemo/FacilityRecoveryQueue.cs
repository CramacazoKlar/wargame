using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CampaignDemo
{
 public sealed class FacilityRecoveryQueue:MonoBehaviour
 {
  readonly List<BattleUnit> queue=new List<BattleUnit>();
  BattleWorld world;
  public static FacilityRecoveryQueue For(BattleUnit u){return u==null||u.Campaign==null?null:For(u.Campaign.world);}
  public static FacilityRecoveryQueue For(BattleWorld w){if(w==null||!LocalPrototypeOrders.EnabledFor(w))return null;var q=w.GetComponent<FacilityRecoveryQueue>();if(q==null)q=w.gameObject.AddComponent<FacilityRecoveryQueue>();q.world=w;return q;}
  public static RecoveryPoint ResourcePool(BattleWorld w,RecoveryPoint point){if(point==null||!point.isRepairSite||point.resourceStock>0||w==null)return point;return w.points.Where(p=>p!=null&&!p.isRepairSite&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId==point.ownerId).OrderBy(p=>Vector2.Distance(point.transform.position,p.transform.position)).FirstOrDefault()??point;}
  public string[] NetworkQueue(string owner){return queue.Where(u=>u!=null&&u.ownerId==owner).Select(u=>u.unitId).ToArray();}
  public void ApplyNetworkQueue(string[] ids){queue.Clear();if(ids==null)return;foreach(var id in ids){var u=world.units.FirstOrDefault(x=>x!=null&&x.unitId==id);if(u!=null)queue.Add(u);}}
  public bool Contains(BattleUnit u){return queue.Contains(u);}
  public int Position(BattleUnit u){if(u==null||u.Point==null)return 0;int n=0;foreach(var member in queue)if(member!=null&&member.Point==u.Point){n++;if(member==u)return n;}return 0;}
  public string Status(BattleUnit u){return !Contains(u)?"":u.State==ArmyBattleState.Repairing?"恢复中":Position(u)==1?"等待恢复资源":"恢复排队第 "+Position(u)+" 位";}
  public bool Enqueue(BattleUnit u){if(u==null||!u.Alive||u.Point==null||u.Point.ownerId!=u.ownerId||u.Fraction>=.999999f||(u.State!=ArmyBattleState.InsidePoint&&u.State!=ArmyBattleState.Repairing))return false;if(!queue.Contains(u))queue.Add(u);return true;}
  public void Cancel(BattleUnit u){queue.Remove(u);if(u!=null)u.CancelRepair();}
  public bool Toggle(BattleUnit u){if(Contains(u)){Cancel(u);return true;}return Enqueue(u);}
  public void Before(){
   queue.RemoveAll(u=>u==null||!u.Alive||u.Point==null||u.Point.ownerId!=u.ownerId||u.Fraction>=.999999f);
   // Adopt callers using the existing BeginRepair API, then serialize work per facility.
   foreach(var u in world.units)if(u!=null&&u.State==ArmyBattleState.Repairing&&u.Point!=null&&u.Point.ownerId==u.ownerId&&!queue.Contains(u))Enqueue(u);
   var active=new HashSet<RecoveryPoint>();foreach(var u in queue){var point=u.Point;var payer=ResourcePool(world,point);bool first=active.Add(point);if(first&&(point.resourcePerPerson<=0||payer.resourceStock>0)){if(u.State==ArmyBattleState.InsidePoint)u.BeginRepair();}else u.CancelRepair();}
  }
  public void After(){foreach(var u in queue.ToArray())if(u==null||!u.Alive||u.Point==null||u.Point.ownerId!=u.ownerId||u.Fraction>=.999999f)Cancel(u);}
 }
}
