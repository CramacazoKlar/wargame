using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class TacticalMapDisplay : MonoBehaviour
    {
        public Camera mapCamera;
        public MouseModeStateMachine modes;
        public ArmyRoadMover[] armies;
        public OwnershipMapRuntime ownershipMap;
        public Color visionColor=new Color(0.2f,0.8f,1);
        public Color attackColor=new Color(1,0.6f,0.15f);
        [Range(12,128)] public int circleSegments=64;
        public float lineWidth=0.04f;
        sealed class Ring
        {
            public ArmyTacticalInfo info;
            public RoadRouteView view;
            public readonly List<RoadPoint> points=new List<RoadPoint>();
            public float radius=-1;
            public Vector3 position;
            public int segments;
        }
        readonly List<Ring> rings=new List<Ring>();
        readonly List<RoadRouteView> regions=new List<RoadRouteView>();
        OwnershipMapDefinition drawnDefinition;
        int drawnRevision=-1;
        Matrix4x4 drawnMatrix;
        TacticalMapLayer lastLayer=(TacticalMapLayer)(-1);

        public void Initialize()
        {
            if(mapCamera==null||modes==null||armies==null) { enabled=false; return; }
            foreach(var army in armies)
            {
                if(army==null) continue;
                var info=army.GetComponent<ArmyTacticalInfo>();
                if(info==null) info=army.gameObject.AddComponent<ArmyTacticalInfo>();
                var view=RoadRouteView.Create(transform,"Range "+army.name,lineWidth,5,visionColor);
                view.gameObject.SetActive(false); rings.Add(new Ring{info=info,view=view});
            }
        }
        void LateUpdate()
        {
            if(modes==null||mapCamera==null) return;
            var layer=modes.DisplayLayer;
            bool range=layer==TacticalMapLayer.Vision||layer==TacticalMapLayer.Attack;
            foreach(var ring in rings)
            {
                if(ring.view==null) continue;
                bool visible=range&&ring.info!=null&&ring.info.gameObject.activeInHierarchy;
                if(ring.view.gameObject.activeSelf!=visible) ring.view.gameObject.SetActive(visible);
                if(!visible) continue;
                float radius=Mathf.Max(0,layer==TacticalMapLayer.Vision?ring.info.visionRadius:ring.info.attackRadius);
                Vector3 p=ring.info.transform.position;
                int count=Mathf.Clamp(circleSegments,12,128);
                if(radius!=ring.radius||p!=ring.position||count!=ring.segments||layer!=lastLayer)
                {
                    ring.points.Clear();
                    for(int i=0;i<=count;i++)
                    { float angle=2*Mathf.PI*i/count; ring.points.Add(new RoadPoint(p.x+Mathf.Cos(angle)*radius,p.y+Mathf.Sin(angle)*radius)); }
                    ring.view.SetColor(layer==TacticalMapLayer.Vision?visionColor:attackColor); ring.view.Show(ring.points);
                    ring.radius=radius; ring.position=p; ring.segments=count;
                }
            }
            if(layer==TacticalMapLayer.Ownership) RefreshOwnership();
            foreach(var view in regions) if(view!=null&&view.gameObject.activeSelf!=(layer==TacticalMapLayer.Ownership)) view.gameObject.SetActive(layer==TacticalMapLayer.Ownership);
            lastLayer=layer;
        }
        void RefreshOwnership()
        {
            var definition=ownershipMap==null?null:ownershipMap.definition;
            var matrix=ownershipMap==null?Matrix4x4.identity:ownershipMap.transform.localToWorldMatrix;
            if(definition==drawnDefinition&&(definition==null||definition.Revision==drawnRevision)&&matrix==drawnMatrix) return;
            foreach(var view in regions) if(view!=null) Destroy(view.gameObject);
            regions.Clear(); drawnDefinition=definition; drawnRevision=definition==null?-1:definition.Revision; drawnMatrix=matrix;
            if(definition==null) return;
            foreach(var region in definition.regions??new OwnershipMapDefinition.Region[0])
            {
                if(region==null||region.polygon==null||region.polygon.Length<3) continue;
                var points=new List<RoadPoint>();
                foreach(var p in region.polygon)
                { var at=ownershipMap.transform.TransformPoint(p); points.Add(new RoadPoint(at.x,at.y)); }
                points.Add(points[0]);
                var color=region.state==OwnershipState.Unoccupied?Color.gray:region.color;
                var view=RoadRouteView.Create(transform,"Territory "+region.name,lineWidth*2,5,color);
                view.Show(points); regions.Add(view);
            }
        }
        void OnGUI()
        {
            if(modes==null||mapCamera==null||modes.CurrentMode!=MouseCommandMode.Interaction) return;
            GUI.Label(new Rect(20,105,550,25),"3: "+modes.DisplayLayer+" | Press 3 to cycle display layers");
            if(modes.DisplayLayer==TacticalMapLayer.ArmyStatus)
            foreach(var army in armies??new ArmyRoadMover[0])
            {
                if(army==null||!army.gameObject.activeInHierarchy) continue;
                var info=army.GetComponent<ArmyTacticalInfo>(); var motor=army.Motor;
                string state=motor==null?"Not initialized":motor.PlanningPaused?"Planning":motor.Blocked?"Blocked":motor.IsMoving?"Marching ("+motor.MovementMode+")":"Idle";
                Label(army.transform.position,(info==null?army.name:info.DisplayName)+" | "+state+
                    " | speed "+(motor==null?0:motor.CurrentSpeed).ToString("0.00")+(info==null?"":" | Player "+info.ownerId+" "+info.extraStatus));
            }
            if(modes.DisplayLayer==TacticalMapLayer.Ownership&&ownershipMap!=null&&ownershipMap.definition!=null)
            foreach(var region in ownershipMap.definition.regions??new OwnershipMapDefinition.Region[0])
            {
                if(region==null||region.polygon==null||region.polygon.Length<3) continue;
                Vector2 center=Vector2.zero; foreach(var point in region.polygon) center+=point; center/=region.polygon.Length;
                Label(ownershipMap.transform.TransformPoint(center),region.name+" | "+
                    (region.state==OwnershipState.Unoccupied?"Unoccupied":"Occupied: Player "+region.ownerId));
            }
        }
        void Label(Vector3 world,string text)
        { var screen=mapCamera.WorldToScreenPoint(world); if(screen.z>0) GUI.Label(new Rect(screen.x-60,Screen.height-screen.y+14,350,45),text); }
        void OnDisable()
        { foreach(var ring in rings) if(ring.view!=null) ring.view.gameObject.SetActive(false); foreach(var view in regions) if(view!=null) view.gameObject.SetActive(false); }
    }
}
