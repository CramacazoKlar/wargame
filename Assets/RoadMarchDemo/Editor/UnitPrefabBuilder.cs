using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
public static class UnitPrefabBuilder {
 const string Root="Assets/Game/UnitLibrary";
 static string[] Names={"Infantry","Engineer","Scout","Tank","Artillery","MountainInfantry","PersonnelCarrier","TransportTruck"};
 static UnitRole[] Roles={UnitRole.Infantry,UnitRole.Engineer,UnitRole.Scout,UnitRole.Tank,UnitRole.Artillery,UnitRole.MountainInfantry,UnitRole.PersonnelCarrier,UnitRole.Logistics};
 static string[] Labels={"步兵","工兵","侦察兵","坦克","炮兵","山地步兵","运兵车","资源运输车"};
 static string[] Echelons={"班","排","连","营","团"};
 static T Load<T>(string p)where T:UnityEngine.Object {var v=AssetDatabase.LoadAssetAtPath<T>(p);if(v==null)throw new Exception("Missing "+p);return v;}
 public static void Build(){
  Directory.CreateDirectory(Root+"/Sprites");Directory.CreateDirectory(Root+"/Combat");Directory.CreateDirectory(Root+"/Prefabs/Friendly");Directory.CreateDirectory(Root+"/Prefabs/Enemy");Directory.CreateDirectory("Assets/Resources");
  var sheet=new Texture2D(2,2);sheet.LoadImage(File.ReadAllBytes("Assets/Game/UnitLibrary/SourceSheet.png"));
  var catalog=ScriptableObject.CreateInstance<UnitSymbolCatalog>();
  for(int side=0;side<2;side++)for(int row=0;row<8;row++)for(int grade=0;grade<5;grade++){
   string id=(side==0?"Friendly_":"Enemy_")+Names[row]+"_"+(ArmyEchelon)grade;
   float cx=(side==0?218:1118)+145*grade;float cy=178+127*row;
   var sprite=Crop(sheet,id,cx-68,cy-64,136,108,new Vector2(.5f,44f/108));
   (side==0?catalog.friendly:catalog.enemy)[row*5+grade]=sprite;
  }
  catalog.friendlyCity=Crop(sheet,"Friendly_City",175,1275,86,70,new Vector2(.5f,.5f));
  catalog.enemyCity=Crop(sheet,"Enemy_City",1075,1275,86,70,new Vector2(.5f,.5f));
  AssetDatabase.CreateAsset(catalog,"Assets/Resources/UnitSymbols.asset");
  for(int row=0;row<8;row++)for(int grade=0;grade<5;grade++){
   var combat=UnityEngine.Object.Instantiate(BaseCombat(row));combat.echelon=(ArmyEchelon)grade;combat.role=Roles[row];combat.name=Names[row]+"_"+(ArmyEchelon)grade;
   AssetDatabase.CreateAsset(combat,Root+"/Combat/"+combat.name+".asset");
   for(int side=0;side<2;side++){
    var g=new GameObject((side==0?"己方_":"敌方_")+Labels[row]+Echelons[grade]);
    var r=g.AddComponent<SpriteRenderer>();r.sprite=catalog.Unit(Roles[row],(ArmyEchelon)grade,side==0);r.color=Color.white;r.sortingOrder=10;
    var m=g.AddComponent<ArmyRoadMover>();m.definition=Movement(row);
    var f=g.AddComponent<ArmyFreeMarchMover>();f.definition=m.definition.freeMarchDefinition;
    var u=g.AddComponent<BattleUnit>();u.combatDefinition=combat;u.ownerId=side==0?"A":"B";u.unitId=g.name+"-template";u.designation=Labels[row]+Echelons[grade];
    g.AddComponent<CampaignUnit>();g.AddComponent<ArmyRouteMotor>();g.AddComponent<ArmyTacticalInfo>();g.AddComponent<BattleUnitView>();
    var identity=g.AddComponent<UnitSymbolIdentity>();identity.catalog=catalog;
    if(row==4)g.AddComponent<ArtilleryBattery>().definition=Load<ArtilleryDefinition>("Assets/Game/Data/CombinedArms/DefaultArtillery.asset");
    if(row==6)g.AddComponent<PersonnelCarrier>();
    if(row==7)g.AddComponent<TransportTruck>().definition=Load<TransportDefinition>("Assets/Game/Data/Logistics/DefaultTransport.asset");
    PrefabUtility.SaveAsPrefabAsset(g,Root+"/Prefabs/"+(side==0?"Friendly/":"Enemy/")+Names[row]+"_"+(ArmyEchelon)grade+".prefab");UnityEngine.Object.DestroyImmediate(g);
   }
  }
  var rules=Load<CampaignRules>("Assets/Game/Data/Campaign/CampaignRules.asset");
  for(int side=0;side<2;side++)for(int kind=1;kind<2;kind++){
   var g=new GameObject((side==0?"己方_":"敌方_")+"城市");var r=g.AddComponent<SpriteRenderer>();r.sortingOrder=8;r.color=Color.white;r.sprite=side==0?catalog.friendlyCity:catalog.enemyCity;
   g.AddComponent<UnitSymbolIdentity>().catalog=catalog;
   {var p=g.AddComponent<RecoveryPoint>();p.ownerId=side==0?"A":"B";p.displayName=g.name;p.kind=FacilityKind.RecoveryPoint;}
   PrefabUtility.SaveAsPrefabAsset(g,Root+"/Prefabs/"+(side==0?"Friendly/":"Enemy/")+"City"+".prefab");UnityEngine.Object.DestroyImmediate(g);
  }
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
  foreach(var u in UnityEngine.Object.FindObjectsOfType<BattleUnit>()){var i=u.GetComponent<UnitSymbolIdentity>();if(i==null)i=u.gameObject.AddComponent<UnitSymbolIdentity>();i.catalog=catalog;var r=u.GetComponent<SpriteRenderer>();r.sprite=catalog.Unit(u.combatDefinition.role,u.combatDefinition.echelon,u.ownerId=="A");r.color=Color.white;u.transform.localScale=Vector3.one;EditorUtility.SetDirty(u);}
  foreach(var p in UnityEngine.Object.FindObjectsOfType<RecoveryPoint>()){var i=p.GetComponent<UnitSymbolIdentity>();if(i==null)i=p.gameObject.AddComponent<UnitSymbolIdentity>();i.catalog=catalog;var r=p.GetComponent<SpriteRenderer>();if(r!=null){r.sprite=p.ownerId=="A"?catalog.friendlyCity:catalog.enemyCity;r.color=Color.white;}}
  var logistics=UnityEngine.Object.FindObjectOfType<LogisticsWorld>();logistics.truckPrefab=Load<GameObject>(Root+"/Prefabs/Friendly/TransportTruck_Company.prefab");
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Verify();
 }
 static ArmyDefinition Movement(int row){return Load<ArmyDefinition>(row==0?"Assets/Game/Data/Infantry.asset":row==3?"Assets/Game/Data/Tank.asset":row==7?"Assets/Game/Data/Logistics/TruckMovement.asset":"Assets/Game/Data/CombinedArms/"+Names[row]+"Movement.asset");}
 static ArmyCombatDefinition BaseCombat(int row){return Load<ArmyCombatDefinition>(row==0?"Assets/Game/Data/Combat/InfantryRegiment.asset":row==3?"Assets/Game/Data/Combat/ArmorRegiment.asset":row==7?"Assets/Game/Data/Logistics/TruckCombat.asset":"Assets/Game/Data/CombinedArms/"+Names[row]+"Combat.asset");}
 static Sprite Crop(Texture2D sheet,string id,float x,float top,float w,float h,Vector2 pivot){
  int left=Mathf.RoundToInt(x/1800*sheet.width),bottom=Mathf.RoundToInt((1400-top-h)/1400*sheet.height),width=Mathf.RoundToInt(w/1800*sheet.width),height=Mathf.RoundToInt(h/1400*sheet.height);
  var t=new Texture2D(width,height,TextureFormat.RGBA32,false);var pixels=sheet.GetPixels(left,bottom,width,height);for(int i=0;i<pixels.Length;i++){var c=pixels[i];float minimum=Mathf.Min(c.r,Mathf.Min(c.g,c.b)),maximum=Mathf.Max(c.r,Mathf.Max(c.g,c.b));if(minimum>.96f)c=Color.clear;else if(maximum-minimum<.035f)c=new Color(0,0,0,1-minimum);pixels[i]=c;}if(!id.Contains("Collector")&&!id.Contains("City"))pixels=UnitSymbolStrokeUtility.Thicken(pixels,width,height);t.SetPixels(pixels);t.Apply();string path=Root+"/Sprites/"+id+".png";File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=150*sheet.width/1800;importer.spritePivot=pivot;var s=new TextureImporterSettings();importer.ReadTextureSettings(s);s.spriteAlignment=(int)SpriteAlignment.Custom;s.spritePivot=pivot;importer.SetTextureSettings(s);importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return Load<Sprite>(path);
 }
 public static void Verify(){
  int n=0;foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"})){
   var g=Load<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var u=g.GetComponent<BattleUnit>();if(g.GetComponent<SpriteRenderer>().sprite==null||g.GetComponent<UnitSymbolIdentity>().catalog==null)throw new Exception("Missing visual "+g.name);
   if(u!=null){if(u.combatDefinition==null||g.GetComponent<ArmyRoadMover>().definition==null||g.GetComponent<ArmyFreeMarchMover>().definition==null||g.GetComponent<CampaignUnit>()==null)throw new Exception("Missing behavior "+g.name);if(u.combatDefinition.role==UnitRole.Artillery&&g.GetComponent<ArtilleryBattery>().definition==null)throw new Exception("Missing artillery");}n++;
  }if(n!=84)throw new Exception("Prefab count "+n);Debug.Log("UNIT_LIBRARY_VERIFIED: 84 prefabs, 40 external combat definitions, 84 sprites");
 }
}

