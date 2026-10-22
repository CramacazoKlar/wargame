using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor { public static class DoubleValleyFinalize {
static Vector2 P(float x,float y){return new Vector2((x-9)*96/18,(y-6)*5);}
public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");var world=Object.FindObjectOfType<BattleWorld>();var setup=world.setup;
if(setup.ownershipMap!=null&&setup.ownershipMap.definition!=null){var own=Object.Instantiate(setup.ownershipMap.definition);own.name="Ownership";foreach(var r in own.regions)for(int i=0;i<r.polygon.Length;i++)r.polygon[i]*=3;AssetDatabase.CreateAsset(own,"Assets/Game/DoubleValley/Ownership.asset");setup.ownershipMap.definition=own;}
foreach(var f in Object.FindObjectsOfType<Fortification>())Object.DestroyImmediate(f.gameObject);
var sites=Object.FindObjectsOfType<ResourceSite>();foreach(var s in sites)Object.DestroyImmediate(s.gameObject);string[] names={"北望矿场","青石料场","东原矿场","南谷矿场","东岭仓库","白榆仓库","西林仓库","松溪仓库"};float[] xy={8.3f,9.5f,7.7f,5.5f,11.2f,5.6f,10.5f,2.2f,15.5f,7.2f,15.6f,4.4f,2.3f,7.3f,2.3f,5};var list=new System.Collections.Generic.List<ResourceSite>();for(int i=0;i<8;i++){var g=new GameObject(names[i]);g.transform.position=P(xy[i*2],xy[i*2+1]);var s=g.AddComponent<ResourceSite>();s.siteId="DV-R"+(i+1);s.displayName=names[i];s.areaSize=new Vector2(1.5f,1.5f);list.Add(s);}var logistics=Object.FindObjectOfType<LogisticsWorld>();if(logistics!=null)logistics.sites=list.ToArray();
var cart=Object.FindObjectOfType<StaffMapCartography>();cart.Rebuild();File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-19/c-desktop-wargame-9/outputs/DoubleValley9x.png",cart.Texture.EncodeToPNG());EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("DOUBLE_VALLEY_FINALIZED");}
}}
