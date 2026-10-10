using UnityEngine;
namespace CampaignDemo {
 [CreateAssetMenu(menuName="Campaign Demo/Unit Symbols")]
 public sealed class UnitSymbolCatalog:ScriptableObject {
  public Sprite[] friendly=new Sprite[40], enemy=new Sprite[40];
  public Sprite friendlyCollector,enemyCollector,friendlyCity,enemyCity,enemyUnknown;
  public Sprite Unit(UnitRole role,ArmyEchelon echelon,bool own) {
   int row=role==UnitRole.Infantry?0:role==UnitRole.Engineer?1:role==UnitRole.Scout?2:role==UnitRole.Tank?3:role==UnitRole.Artillery?4:role==UnitRole.MountainInfantry?5:role==UnitRole.PersonnelCarrier?6:7;
   var a=own?friendly:enemy; int i=row*5+Mathf.Clamp((int)echelon,0,4);return a!=null&&i<a.Length?a[i]:null;
  }
 }
}

