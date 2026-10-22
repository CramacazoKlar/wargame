using UnityEngine;
namespace CampaignDemo {
 // High-resolution anti-aliased source images with simplified small-size variants.
 [DefaultExecutionOrder(1100),DisallowMultipleComponent]
 public sealed class SmoothUnitSymbolView:MonoBehaviour {
  public SmoothUnitSymbols symbols;
  BattleUnit unit;SpriteRenderer view;
  void Awake(){unit=GetComponent<BattleUnit>();view=GetComponent<SpriteRenderer>();}
  public void Refresh(){if(unit==null)unit=GetComponent<BattleUnit>();if(view==null)view=GetComponent<SpriteRenderer>();if(unit==null||view==null||symbols==null)return;var w=unit.Campaign==null?null:unit.Campaign.world;var presentation=w==null?null:w.GetComponent<StaffMapPresentation>();int pixels=Mathf.Max(16,presentation==null?44:presentation.SymbolPixels);var sprite=symbols.Select(pixels,unit.ownerId);if(sprite==null)return;if(w!=null&&unit.ownerId!=w.currentPlayer&&!w.IsKnown(w.currentPlayer,unit))sprite=StaffMapSymbols.Unit(unit.combatDefinition.role,unit.combatDefinition.echelon,unit.ownerId==BattleFactions.BluePlayerId,pixels,true);view.sprite=sprite;view.color=Color.white;var camera=w==null||w.setup==null?Camera.main:w.setup.mapCamera;if(camera==null||!camera.orthographic)return;float width=pixels*2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight);var scale=BattleUnitView.SymbolScale(transform);view.drawMode=SpriteDrawMode.Sliced;view.size=new Vector2(width/Mathf.Max(.0001f,scale.x),width*sprite.rect.height/sprite.rect.width/Mathf.Max(.0001f,scale.y));}
  void LateUpdate(){Refresh();}
 }
}
