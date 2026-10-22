using UnityEngine;
namespace CampaignDemo {
 [CreateAssetMenu(menuName="Campaign Demo/平滑军标")]
 public sealed class SmoothUnitSymbols:ScriptableObject {
  public int[] displayWidths={16,24,32,44,64,128};
  public Sprite[] blue,red,neutral;
  public Sprite Select(int pixels,string owner){var list=owner==BattleFactions.BluePlayerId?blue:owner==BattleFactions.RedPlayerId?red:neutral;if(list==null||list.Length==0)return null;int i=0;while(i<displayWidths.Length-1&&displayWidths[i]<pixels)i++;return list[Mathf.Min(i,list.Length-1)];}
 }
}
