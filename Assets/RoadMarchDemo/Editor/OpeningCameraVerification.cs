using System;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor {
 public static class OpeningCameraVerification {
  public static void Run(){var root=new GameObject("camera test");try{var cameraObject=new GameObject("test camera");cameraObject.transform.SetParent(root.transform);var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=7;camera.transform.position=new Vector3(100,100,-10);var points=new RecoveryPoint[5];for(int i=0;i<5;i++){var g=new GameObject("city"+i);g.transform.SetParent(root.transform);points[i]=g.AddComponent<RecoveryPoint>();points[i].kind=FacilityKind.RecoveryPoint;}points[0].ownerId=points[1].ownerId="A";points[0].transform.position=new Vector3(2,4);points[1].transform.position=new Vector3(6,8);points[2].ownerId=points[3].ownerId="B";points[2].transform.position=new Vector3(-8,-4);points[3].transform.position=new Vector3(-4,-8);points[4].ownerId="B";points[4].kind=FacilityKind.Mine;points[4].transform.position=Vector3.one*999;
   Check(OpeningCameraFocus.Focus(camera,points,"A")&&camera.transform.position==new Vector3(4,6,-10),"blue cities center");Check(OpeningCameraFocus.Focus(camera,points,"B")&&camera.transform.position==new Vector3(-6,-6,-10),"red cities center; exclude mines");Check(camera.orthographicSize==7,"preserve zoom");Check(!OpeningCameraFocus.Focus(camera,points,"none")&&camera.transform.position==new Vector3(-6,-6,-10),"no cities preserves camera");Debug.Log("OPENING_CAMERA_PASS: blue/red city centers, mine exclusion, zoom and depth, no-city fallback");
  }finally{UnityEngine.Object.DestroyImmediate(root);}}
  static void Check(bool result,string label){if(!result)throw new Exception(label);}
 }
}
