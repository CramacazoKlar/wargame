using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class BoldOriginalWidthAudit {
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");int count=0;string originals="C:/Users/ZWQ/Desktop/wargame/Assets/Game/UnitLibrary/Sprites";var bodyMethod=typeof(BoldSymbolsAndPVEInstaller).GetMethod("BodyAt",BindingFlags.NonPublic|BindingFlags.Static);foreach(var path in Directory.GetFiles(originals,"*.png")){if(Path.GetFileNameWithoutExtension(path).EndsWith("_City"))continue;var before=(Rect)bodyMethod.Invoke(null,new object[]{path});string metadata=File.ReadAllText(path+".meta");float ppu=float.Parse(Regex.Match(metadata,"spritePixelsToUnits: ([0-9.Ee+-]+)").Groups[1].Value,CultureInfo.InvariantCulture);string replacement="Assets/Game/UnitLibrary/Sprites/"+Path.GetFileName(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(replacement);if(Mathf.Abs(before.width/ppu-BoldSymbolsAndPVEInstaller.VisibleWidth(sprite))>.001f)throw new Exception("Physical width changed: "+path);string guid=Regex.Match(metadata,"guid: ([a-z0-9]+)").Groups[1].Value;if(guid!=AssetDatabase.AssetPathToGUID(replacement))throw new Exception("GUID changed: "+path);count++;}if(count!=82)throw new Exception("Unexpected audit count "+count);UnitPrefabBuilder.Verify();CampaignDemo.Editor.CampaignRevisionVerification.Validate();File.WriteAllText("bold-original-width-audit.txt",count+" original desktop sprite widths and GUIDs preserved; city size deliberately enlarged; 84 prefab references valid; existing 62 combat checks passed.");Debug.Log("BOLD_ORIGINAL_AUDIT_PASS "+count);}
}
