using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampaignDemo
{
    public sealed class RoadMarchSceneSetup : MonoBehaviour
    {
        [Header("User-authored maps")]
        public RoadMapRuntime roadMap;
        public FreeMarchMapRuntime freeMarchMap;
        public OwnershipMapRuntime ownershipMap;
        [Header("User-created camera and armies")]
        public Camera mapCamera;
        public ArmyRoadMover[] sceneArmies;
        public RoadMarchSettings settings;
        [Header("Optional components")]
        public MouseModeStateMachine modes;
        public MapZoomController zoomController;
        public MapPanController panController;
        public TacticalMapDisplay tacticalDisplay;
        public bool showDebugUI;
        public bool showTacticalOverlays=true; public bool showCommittedRoutes=true;
        bool initialized;
        readonly List<Behaviour> managed=new List<Behaviour>();
        readonly List<RoadRouteView> views=new List<RoadRouteView>();

        void Start()
        {
            if(initialized) return;
            try { Validate(); Assemble(); initialized=true; }
            catch(Exception exception)
            {
                foreach(var component in managed) if(component!=null) component.enabled=false;
                foreach(var view in views) if(view!=null) Destroy(view.gameObject);
                Debug.LogError("Scene initialization failed: "+exception.Message,this); enabled=false;
            }
        }
        void Validate()
        {
            if(roadMap==null||roadMap.definition==null) throw new ArgumentException("Assign Road Map and its Definition.");
            if(freeMarchMap==null||freeMarchMap.definition==null) throw new ArgumentException("Assign Free March Map and its Definition.");
            if(mapCamera==null||!mapCamera.orthographic) throw new ArgumentException("Assign the orthographic map camera.");
            if(settings==null) throw new ArgumentException("Assign Road March Settings.");
            if(sceneArmies==null||sceneArmies.Length==0) throw new ArgumentException("Assign your scene armies.");
            var unique=new HashSet<ArmyRoadMover>();
            foreach(var army in sceneArmies)
            {
                if(army==null||army.definition==null) throw new ArgumentException("An army or its Definition is missing.");
                if(!unique.Add(army)) throw new ArgumentException("Duplicate army: "+army.name);
            }
        }
        void Assemble()
        {
            if(roadMap.Network==null) roadMap.Initialize();
            if(roadMap.Network==null||roadMap.Network.Edges.Length==0) throw new ArgumentException("Draw at least one road.");
            modes=modes!=null?modes:GetOrAdd<MouseModeStateMachine>();
            var selection=GetOrAdd<ArmySelection>();
            selection.mapCamera=mapCamera; selection.settings=settings; selection.armies=sceneArmies;
            foreach(var army in sceneArmies)
            {
                var at=army.transform.position;
                army.InitializeAt(roadMap.Network,new RoadPoint(at.x,at.y),View(army.name+" route",0.06f,3,new Color(0.05f,0.45f,0.7f)));
                managed.Add(army.Motor);
                var free=army.GetComponent<ArmyFreeMarchMover>();
                if(free==null) free=army.gameObject.AddComponent<ArmyFreeMarchMover>();
                if(free.definition==null) free.definition=army.definition.freeMarchDefinition;
                free.Initialize(army.Motor);
                free.BindMap(freeMarchMap);
            }
            var road=GetOrAdd<RoadMarchController>();
            road.map=roadMap; road.mapCamera=mapCamera; road.settings=settings; road.selection=selection;
            road.preview=View("Road preview",0.09f,4,settings.validRouteColor); road.BindModes(modes); road.Initialize(); road.enabled=true;
            var freeController=GetOrAdd<FreeMarchController>();
            freeController.map=freeMarchMap; freeController.mapCamera=mapCamera; freeController.settings=settings;
            freeController.selection=selection; freeController.preview=View("Free preview",0.09f,4,settings.validRouteColor);
            freeController.BindModes(modes); freeController.enabled=true;
            zoomController=zoomController!=null?zoomController:GetOrAdd<MapZoomController>();
            zoomController.mapCamera=mapCamera; zoomController.enabled=true;
            panController=panController!=null?panController:GetOrAdd<MapPanController>();
            panController.mapCamera=mapCamera; panController.enabled=true;
            tacticalDisplay=tacticalDisplay!=null?tacticalDisplay:GetOrAdd<TacticalMapDisplay>();
            tacticalDisplay.mapCamera=mapCamera; tacticalDisplay.modes=modes; tacticalDisplay.armies=sceneArmies;
            tacticalDisplay.ownershipMap=ownershipMap; tacticalDisplay.Initialize(); tacticalDisplay.enabled=showTacticalOverlays;
            var ui=GetComponent<RoadMarchDebugUI>();
            if(showDebugUI)
            {
                if(ui==null) ui=GetOrAdd<RoadMarchDebugUI>();
                ui.controller=road; ui.freeController=freeController; ui.modes=modes; ui.enabled=true;
            }
            else if(ui!=null) ui.enabled=false;
        }
        T GetOrAdd<T>() where T:Behaviour
        {
            var component=GetComponent<T>(); if(component==null) component=gameObject.AddComponent<T>();
            if(!managed.Contains(component)) managed.Add(component);
            return component;
        }
        RoadRouteView View(string label,float width,int order,Color color)
        { var view=RoadRouteView.Create(transform,label,width,order,color); view.ConfigureArrow(settings,mapCamera,label.EndsWith(" route"));views.Add(view); if(!showCommittedRoutes&&label.EndsWith(" route")) view.GetComponent<LineRenderer>().enabled=false; return view; }
    }
}


