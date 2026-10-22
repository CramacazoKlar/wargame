using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo {
 [DefaultExecutionOrder(-190)] public sealed class SelectedArmyHotkeys:MonoBehaviour {
  [Min(.001f)] public float nudgeWorldStep=.03f;
  [Min(1)] public float nudgeScreenPixels=3;
  [Min(.05f)] public float repeatDelay=.3f,repeatInterval=.15f;
  public string Status {get;private set;}="";public bool SuppressPointer {get;private set;}
  ArmySelection selection;BattleWorld world;Vector2 held;float repeatAt;bool waitForRelease;GameObject panel,canvasObject;Font font;
  bool Available {get{return world!=null&&world.Initialized&&!world.SimulationEnded&&world.setup.modes!=null&&(world.setup.modes.CurrentMode==MouseCommandMode.RoadMarch||world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch);}}
  void Start(){selection=GetComponent<ArmySelection>();world=GetComponent<BattleWorld>();if(!LocalTacticalBattle.EnabledFor(this))BuildButtons();}
  void CancelDrafts(){GetComponent<RoadMarchController>()?.CancelDraft();GetComponent<FreeMarchController>()?.CancelDraft();GetComponent<GroupMarchController>()?.Cancel();if(Input.GetMouseButton(0))SuppressPointer=true;}
  List<string> SelectedIds(){var ids=new List<string>();foreach(var army in selection.SelectedArmies){var unit=army.GetComponent<BattleUnit>();if(unit!=null)ids.Add(unit.unitId);}if(ids.Count==0){var u=world.GetComponent<CampaignHUD>()?.SelectedUnit;if(u!=null&&u.ownerId==world.currentPlayer)ids.Add(u.unitId);}return ids;}
  public CommandResult EmergencyStop(){if(!Available||selection==null)return CommandResult.No("请在行军模式选择部队");var ids=SelectedIds();if(ids.Count==0)return CommandResult.No("没有选中部队");CancelDrafts();world.GetComponent<LocalPrototypeOrders>()?.CancelDraft();world.GetComponent<LocalCoordinatedOrders>()?.CancelPicking();world.GetComponent<CampaignHUD>()?.CancelTool();UnitContextOrders.ConsumePointer();var result=world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.StopSelected,playerId=world.currentPlayer,unitIds=ids});Status=result.pending?"急停已发送，等待开房端确认":result.accepted?"已取消 "+ids.Count+" 支部队的全部任务及关联":result.reason;waitForRelease=true;return result;}
  public CommandResult Nudge(Vector2 direction){if(!Available||selection==null)return CommandResult.No("请在行军模式选择部队");var ids=SelectedIds();if(ids.Count==0)return CommandResult.No("没有选中部队");CancelDrafts();float units=selection.mapCamera==null?nudgeWorldStep:2*selection.mapCamera.orthographicSize/Mathf.Max(1,selection.mapCamera.pixelHeight);float step=Mathf.Min(.25f,Mathf.Min(Mathf.Max(.001f,nudgeWorldStep),units*Mathf.Max(1,nudgeScreenPixels)));var offset=(Vector3)direction.normalized*step;
   var result=world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.NudgeSelected,playerId=world.currentPlayer,unitIds=ids,destination=offset});Status=result.pending?"微调已发送，等待开房端确认":result.accepted?"所选部队保持队形微调":result.reason;return result;
  }
  bool Editing(){var current=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;return current!=null&&(current.GetComponentInParent<InputField>()!=null||current.GetComponentInParent<Slider>()!=null);}
  void Update(){if(!Input.GetMouseButton(0))SuppressPointer=false;if(panel!=null)panel.SetActive(Available&&selection!=null&&selection.SelectedArmies.Count>0);if(!Available||selection==null||Editing()||(LocalPvpSession.Active!=null&&LocalPvpSession.Active.KeyboardCaptured)){held=Vector2.zero;return;}
   Vector2 direction=new Vector2((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),(Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0));if(Input.GetKeyDown(KeyCode.T)){EmergencyStop();return;}if(waitForRelease){if(direction==Vector2.zero)waitForRelease=false;held=Vector2.zero;return;}
   if(direction==Vector2.zero){held=Vector2.zero;return;}if(direction!=held){held=direction;repeatAt=Time.unscaledTime+repeatDelay;Nudge(direction);}else if(Time.unscaledTime>=repeatAt){repeatAt=Time.unscaledTime+repeatInterval;Nudge(direction);}
  }
  void BuildButtons(){canvasObject=new GameObject("Selected army controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);
   panel=new GameObject("急停与微调",typeof(RectTransform));panel.transform.SetParent(canvasObject.transform,false);var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(12,94);rect.sizeDelta=new Vector2(114,103);
   Button("急停 T",new Vector2(0,72),new Vector2(114,28),()=>EmergencyStop());Button("↑",new Vector2(39,36),Vector2.one*32,()=>Nudge(Vector2.up));Button("←",Vector2.zero,Vector2.one*32,()=>Nudge(Vector2.left));Button("↓",new Vector2(39,0),Vector2.one*32,()=>Nudge(Vector2.down));Button("→",new Vector2(78,0),Vector2.one*32,()=>Nudge(Vector2.right));panel.SetActive(false);
  }
  void Button(string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action){var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(panel.transform,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;go.GetComponent<Image>().color=title=="急停 T"?new Color(.48f,.15f,.1f,.95f):new Color(.25f,.22f,.15f,.95f);var button=go.GetComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);var textObject=new GameObject("Text",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(go.transform,false);var tr=textObject.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;var text=textObject.GetComponent<Text>();text.font=font;text.fontSize=14;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.text=title;text.raycastTarget=false;}
  void OnDisable(){held=Vector2.zero;SuppressPointer=false;if(panel!=null)panel.SetActive(false);}
  void OnDestroy(){if(canvasObject!=null)Destroy(canvasObject);if(font!=null)Destroy(font);}
 }
}

