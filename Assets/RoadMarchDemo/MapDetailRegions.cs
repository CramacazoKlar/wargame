using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Authoring data and a baked sprite only: no random generation during play.
 [DisallowMultipleComponent] public sealed class MapDetailRegions:MonoBehaviour {
  public enum DetailKind { Village,Farmland,RuralMix }
  [Serializable]public sealed class Area {
   public string name="乡村区域";
   public Rect bounds;
   public DetailKind kind=DetailKind.RuralMix;
   public int seed=217;
   [Range(.2f,2)]public float density=1;
   [Tooltip("褐色田比例（保留字段名以兼容已有地图）"),Range(0,1)]public float greenFields=.55f;
   [Range(.2f,2)]public float plotScale=1;
  }
  public FreeMarchMapRuntime terrain;
  public RoadMapRuntime roads;
  [Range(1024,4096)]public int resolution=4096;
  public List<Area> areas=new List<Area>();
  public bool generateFarmland=true;
  public SpriteRenderer bakedView;
  [HideInInspector]public int generatedFields,generatedBuildings,generatedVillages;
  public void Resolve(){if(terrain==null)terrain=GetComponent<FreeMarchMapRuntime>();if(roads==null)roads=GetComponent<RoadMapRuntime>();}
 }
}

