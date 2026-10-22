using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [Serializable] public sealed class PvpMessage {
  public int version=NetworkTacticalOrders.Version;public string type,map,code,player,token,reason;public long sequence;public bool accepted;public BattleCommand command;public PvpSnapshot snapshot;
 }
 [Serializable] public sealed class PvpSnapshot {
  public string[] recoveryQueue;public List<PvpWork> works=new List<PvpWork>();public long tick;public float timeMultiplier=1;public float elapsed;public bool ended;public string[] territory;
  public List<PvpSite> sites=new List<PvpSite>();
  public List<PvpUnit> units=new List<PvpUnit>();public List<PvpCity> cities=new List<PvpCity>();public List<PvpDepot> depots=new List<PvpDepot>();public List<PvpFort> forts=new List<PvpFort>();
  public List<PvpProduction> production=new List<PvpProduction>();
  public List<PvpEffect> effects=new List<PvpEffect>();
 }
 [Serializable] public sealed class PvpUnit {
  public bool rotating,supporting;public string[] links;public string artillery;public int experience,marchStyle;public bool participated,assigned;public string task,coordination;public float capacity,strengthCapacity;public string detachment;public string id,owner,label,point,carrier,combat;public Vector3 position;public bool known,working,hidden,ambush,underFire,moving;public float personnel,timer,speed,desiredSpeed,cargo,artilleryCooldown,artilleryBarrageRemaining,workRemaining;public bool automatic,workApproaching;public ArmyBattleState state;public MouseCommandMode movementMode;public UnitRole role;public ArmyEchelon echelon;public int fortLevel,engineering;public List<RoadPoint> route;
 }
 [Serializable] public sealed class PvpWork {public string worker,owner;public Vector3[] points;public Vector3 side,site;public float total,remaining,length;public int level;public bool approaching;}
 [Serializable] public sealed class PvpSite {public string id;public float stock;}
 [Serializable] public sealed class PvpCity {public string id,owner,capturing;public float stock,progress;public bool factory;}
  [Serializable] public sealed class PvpDepot {public string id,owner,name;public Vector3 position;public float stock;public bool pile;}
 [Serializable] public sealed class PvpFort {public string id,owner;public Vector3 position,side;public Vector3[] points;public int level;public float health,maximum,length,radius;}
 [Serializable] public sealed class PvpProduction {public string id,city,owner;public UnitRole role;public ArmyEchelon echelon;public float remaining,cost;public bool logistics,factory;}
 [Serializable] public sealed class PvpEffect {public CombatEffectStage stage;public string source,sourceOwner,target,targetOwner;public UnitRole role;public bool hasSource;public Vector3 from,to;public float radius;}
}
