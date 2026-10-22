using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor {
 public static class RadarRocketInstaller {
  const string Folder="Assets/Resources/Radar";
  static T Asset<T>(string path)where T:ScriptableObject{var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
  static Vector2 V(float x,float y){return new Vector2(x,y);}
  static Sprite Draw(string name,int size,Color ink,bool rocket){
   var c=new StaffMapCanvas(512,384,new Rect(-.65f,-.4875f,1.3f,.975f));float line=size<=16?.09f:size<=24?.065f:.04f;
   if(rocket){Color fill=name.Contains("_Blue_")?StaffMapStyle.BlueFill:name.Contains("_Red_")?StaffMapStyle.RedFill:StaffMapStyle.Panel;c.Rectangle(Vector2.zero,V(.56f,.36f),0,fill);ink=Color.black;}
   var corners=new[]{V(-.56f,-.36f),V(.56f,-.36f),V(.56f,.36f),V(-.56f,.36f)};for(int j=0;j<4;j++)c.Line(corners[j],corners[(j+1)%4],line,ink,1,false);
   if(!rocket){c.Line(V(-.09f,-.29f),V(-.09f,-.025f),line,ink,1,false);c.Line(V(-.15f,-.04f),V(.015f,.105f),line,ink,1,false);int arcs=size<=16?1:2;for(int i=0;i<arcs;i++){float radius=.14f+i*.10f;Vector2 previous=default;for(int j=0;j<=32;j++){float a=j*Mathf.PI/64;var p=V(-.04f,.015f)+V(Mathf.Cos(a),Mathf.Sin(a))*radius;if(j>0)c.Line(previous,p,line,ink,1,false);previous=p;}}}
   else {var tail=V(-.30f,-.10f);var nose=V(.34f,.23f);var direction=(nose-tail).normalized;var normal=V(-direction.y,direction.x);c.Line(tail,nose-direction*.10f,.11f,ink,1,false);for(int j=0;j<=20;j++){float t=j/20f;var at=nose-direction*.10f*(1-t);c.Line(at-normal*.055f*(1-t),at+normal*.055f*(1-t),.008f,ink,1,false);}c.Line(tail-direction*.025f-normal*.055f,tail+normal*.055f,line*.8f,ink,1,false);c.Line(V(-.28f,-.25f),V(.18f,-.015f),line,ink,1,false);c.Line(V(-.04f,-.13f),V(-.04f,-.28f),line,ink,1,false);c.Line(V(-.18f,-.29f),V(.11f,-.29f),line,ink,1,false);}

   for(int i=0;i<c.pixels.Length;i++){var p=c.pixels[i];if(p.a>0){p.r/=p.a;p.g/=p.a;p.b/=p.a;}c.pixels[i]=p;}
   var texture=new Texture2D(c.width,c.height,TextureFormat.RGBA32,false);texture.SetPixels(c.pixels);texture.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=512;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();UnityEngine.Object.DestroyImmediate(texture);return AssetDatabase.LoadAssetAtPath<Sprite>(path);
  }
  static SmoothUnitSymbols Symbols(string name,bool rocket){var set=Asset<SmoothUnitSymbols>(Folder+"/"+name+"Symbols.asset");var widths=new[]{16,24,32,44,64,128};set.displayWidths=widths;set.blue=new Sprite[6];set.red=new Sprite[6];set.neutral=new Sprite[6];for(int i=0;i<6;i++){set.blue[i]=Draw(name+"_Blue_"+widths[i],widths[i],new Color(.055f,.25f,.36f),rocket);set.red[i]=Draw(name+"_Red_"+widths[i],widths[i],new Color(.48f,.09f,.07f),rocket);set.neutral[i]=Draw(name+"_Neutral_"+widths[i],widths[i],new Color(.26f,.27f,.26f),rocket);}EditorUtility.SetDirty(set);return set;}
  public static void RefreshRocketColors(){Symbols("Rocket",true);AssetDatabase.SaveAssets();}
  public static void Run(){
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var radarIcons=Symbols("Radar",false);var rocketIcons=Symbols("Rocket",true);
   var plan=Asset<RadarDefinition>(Folder+"/RadarPlanning.asset");plan.visionRadius=10;plan.health=63;EditorUtility.SetDirty(plan);
   var combat=Asset<ArmyCombatDefinition>(Folder+"/RadarCombat.asset");combat.role=UnitRole.Radar;combat.unitType="雷达";combat.echelon=ArmyEchelon.Company;combat.visionRadius=10;combat.attackRadius=0;combat.fullStrengthAttack=0;combat.squadShotDamage=0;combat.canHideInForest=false;EditorUtility.SetDirty(combat);
   var movement=Asset<ArmyDefinition>(Folder+"/RadarMovement.asset");movement.displayName="固定雷达";movement.speed=.01f;movement.useReferenceTravelTime=false;EditorUtility.SetDirty(movement);
   var go=new GameObject("雷达");var sprite=go.AddComponent<SpriteRenderer>();sprite.sprite=radarIcons.blue[3];sprite.sortingOrder=10;var unit=go.AddComponent<BattleUnit>();unit.combatDefinition=combat;unit.unitId="radar-template";unit.designation="雷达";go.AddComponent<ArmyRoadMover>().definition=movement;go.AddComponent<ArmyFreeMarchMover>();go.AddComponent<ArmyTacticalInfo>();go.AddComponent<BattleUnitView>();go.AddComponent<UnitSymbolIdentity>().catalog=Resources.Load<UnitSymbolCatalog>("UnitSymbols");go.AddComponent<RadarInstallation>().definition=plan;go.AddComponent<SmoothUnitSymbolView>().symbols=radarIcons;PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Radar.prefab");UnityEngine.Object.DestroyImmediate(go);
   const string source="Assets/Game/UnitLibrary/Prefabs/Friendly/Artillery_Company.prefab";go=PrefabUtility.LoadPrefabContents(source);go.name="火箭兵";unit=go.GetComponent<BattleUnit>();var rocketCombat=Asset<ArmyCombatDefinition>(Folder+"/RocketCombat.asset");EditorUtility.CopySerialized(unit.combatDefinition,rocketCombat);rocketCombat.name="RocketCombat";rocketCombat.role=UnitRole.RocketArtillery;rocketCombat.unitType="火箭兵";unit.combatDefinition=rocketCombat;unit.unitId="rocket-template";unit.designation="火箭兵";
   var battery=go.GetComponent<ArtilleryBattery>();var rocketBattery=Asset<ArtilleryDefinition>(Folder+"/RocketArtillery.asset");EditorUtility.CopySerialized(battery.definition,rocketBattery);rocketBattery.name="RocketArtillery";rocketBattery.range=battery.definition.range*2;rocketBattery.requireVision=false;battery.definition=rocketBattery;
   go.AddComponent<SmoothUnitSymbolView>().symbols=rocketIcons;go.GetComponent<SpriteRenderer>().sprite=rocketIcons.blue[3];PrefabUtility.SaveAsPrefabAsset(go,Folder+"/RocketArtillery.prefab");PrefabUtility.UnloadPrefabContents(go);EditorUtility.SetDirty(rocketCombat);EditorUtility.SetDirty(rocketBattery);
   var production=Resources.Load<CityProductionRules>("CityProductionRules");if(production==null)throw new Exception("Missing city production");var original=production.Find(UnitRole.Artillery);var entries=production.additionalUnits.Where(e=>e.role!=UnitRole.RocketArtillery).ToList();entries.Add(new CityProductionRules.Entry{role=UnitRole.RocketArtillery,prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/RocketArtillery.prefab"),squadCost=original.squadCost,squadSeconds=original.squadSeconds,costScale=original.costScale,timeScale=original.timeScale,requiresFactory=original.requiresFactory});production.additionalUnits=entries.ToArray();EditorUtility.SetDirty(production);
   var ranges=Resources.Load<UnitRangePlanning>("UnitRangePlanning");var rows=ranges.roles.Where(e=>e.role!=UnitRole.RocketArtillery).ToList();var artillery=ranges.For(UnitRole.Artillery);rows.Add(new UnitRangePlanning.Entry{role=UnitRole.RocketArtillery,visionRadius=artillery.visionRadius,attackRadius=artillery.attackRadius*2});ranges.roles=rows.ToArray();EditorUtility.SetDirty(ranges);
   foreach(var guid in AssetDatabase.FindAssets("t:ArtilleryDefinition")){var a=AssetDatabase.LoadAssetAtPath<ArtilleryDefinition>(AssetDatabase.GUIDToAssetPath(guid));a.requireVision=false;EditorUtility.SetDirty(a);}
   AssetDatabase.SaveAssets();Debug.Log("RADAR_ROCKET_INSTALL_PASS smooth512Source=true variants=6 factions=3 radarRange=10 rocketRange="+rocketBattery.range);
  }
 }
}

