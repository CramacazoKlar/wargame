using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public sealed class AITaskReservations:MonoBehaviour {
  readonly Dictionary<string,string> units=new Dictionary<string,string>();
  readonly Dictionary<string,Dictionary<string,float>> resources=new Dictionary<string,Dictionary<string,float>>();
  readonly Dictionary<string,KeyValuePair<string,float>> incoming=new Dictionary<string,KeyValuePair<string,float>>();
  public bool ClaimUnit(string task,string unit){if(string.IsNullOrEmpty(task)||string.IsNullOrEmpty(unit))return false;string owner;if(units.TryGetValue(unit,out owner)&&owner!=task)return false;units[unit]=task;return true;}
  public float ReservedAt(string storage){float amount=0;foreach(var row in resources.Values){float value;if(row.TryGetValue(storage,out value))amount+=value;}return amount;}
  public bool Reserve(string task,string storage,float amount,float stock){if(string.IsNullOrEmpty(task)||string.IsNullOrEmpty(storage)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<0)return false;Dictionary<string,float> row;if(!resources.TryGetValue(task,out row))row=new Dictionary<string,float>();float old;row.TryGetValue(storage,out old);if(ReservedAt(storage)-old+amount>stock)return false;row[storage]=amount;resources[task]=row;return true;}
  public void ReleaseResource(string task,string storage){Dictionary<string,float> row;if(resources.TryGetValue(task,out row)){row.Remove(storage);if(row.Count==0)resources.Remove(task);}}
  public void SetIncoming(string task,string storage,float amount){if(string.IsNullOrEmpty(storage)||amount<=0)incoming.Remove(task);else incoming[task]=new KeyValuePair<string,float>(storage,amount);}
  public float IncomingAt(string storage){float value=0;foreach(var item in incoming.Values)if(item.Key==storage)value+=item.Value;return value;}
  public void Release(string task){foreach(var key in new List<string>(units.Keys))if(units[key]==task)units.Remove(key);resources.Remove(task);incoming.Remove(task);}
  public bool HasTask(string task){if(resources.ContainsKey(task)||incoming.ContainsKey(task))return true;foreach(var value in units.Values)if(value==task)return true;return false;}
 }
}
