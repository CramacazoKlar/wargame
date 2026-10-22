using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CampaignDemo
{
    // Shared tactical rules; network orders execute on the authoritative host.
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class LocalPrototypeOrders : MonoBehaviour
    {
        public static bool EnabledFor(Component c) { return LocalTacticalBattle.EnabledFor(c); }
        public static LocalPrototypeOrders For(Component c) { var w = c == null ? null : c.GetComponent<BattleWorld>(); if (w == null && c != null) w = c.GetComponent<BattleUnit>()?.Campaign?.world; return w == null ? null : w.GetComponent<LocalPrototypeOrders>(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene scene, LoadSceneMode mode) { foreach (var w in FindObjectsOfType<BattleWorld>()) if (w.gameObject.scene == scene && EnabledFor(w) && w.GetComponent<LocalPrototypeOrders>() == null) w.gameObject.AddComponent<LocalPrototypeOrders>(); }

        public sealed class Waypoint
        {
            public Vector3 position;
            public ResourceSite site;
            public RecoveryPoint city;
            public bool loadStorage;
            public Vector3 Position { get { return site != null ? site.transform.position : city != null ? city.transform.position : position; } }
        }
        sealed class Job
        {
            public BattleUnit unit;
            public List<Waypoint> nodes;
            public int index;
            public bool repeat, automatic, moving;
            public uint revision;
            public string state;
        }
        BattleWorld world;
        ArmySelection selection;
        CampaignHUD hud;
        readonly Dictionary<BattleUnit, Job> jobs = new Dictionary<BattleUnit, Job>();
        readonly Dictionary<TransportTruck, RecoveryPoint> cityClicks = new Dictionary<TransportTruck, RecoveryPoint>();
        readonly Dictionary<BattleUnit, string> status = new Dictionary<BattleUnit, string>();
        readonly List<BattleUnit> draftUnits = new List<BattleUnit>();
        readonly List<Waypoint> draft = new List<Waypoint>();
        LineRenderer preview;
        Material material;
        public bool Drafting { get; private set; }
        public enum DraftKind { Patrol,Road,Straight }
        public DraftKind CurrentDraft {get;private set;}
        public int JobCount { get { return jobs.Count; } }
        public int DraftCount { get { return draft.Count; } }
        void Awake() { Resolve(); }
        void Resolve() { if (world == null) world = GetComponent<BattleWorld>(); if (selection == null) selection = GetComponent<ArmySelection>(); if (hud == null) hud = FindObjectOfType<CampaignHUD>(); }
        public bool IsAssigned(BattleUnit unit) { return unit != null && (NetworkTacticalOrders.Authority?jobs.ContainsKey(unit):unit.GetComponent<NetworkUnitPresentation>()?.assigned==true); }
        public string Status(BattleUnit unit) { return !NetworkTacticalOrders.Authority?(unit==null?"待命":unit.GetComponent<NetworkUnitPresentation>()?.task??"待命"):unit != null && status.TryGetValue(unit, out var value) ? value : "待命"; }
        void Feedback(string value) { hud?.SetCommandFeedback(value); }
        bool OverUI() { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); }
        bool Editing() { var o = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject; return o != null && o.GetComponent<InputField>() != null; }
        Vector3 MouseWorld() { var c = world.setup.mapCamera; var p = MapProjection.Ground(c,new Vector2(Input.mousePosition.x,Input.mousePosition.y)); p.z = 0; return p; }
        public Waypoint PointAt(Vector2 screen, Vector3 ground)
        {
            var node = new Waypoint { position = ground }; float distance = world.setup.settings.selectionPixels;
            foreach (var site in world.campaign.logistics.sites) if (site != null) { var p = world.setup.mapCamera.WorldToScreenPoint(site.transform.position); float d = Vector2.Distance(screen, p); if (p.z > 0 && d < distance) { distance = d; node.site = site; node.city = null; } }
            foreach (var city in world.points) if (city != null && city.kind == FacilityKind.RecoveryPoint) { var p = world.setup.mapCamera.WorldToScreenPoint(city.transform.position); float d = Vector2.Distance(screen, p); if (p.z > 0 && d < distance) { distance = d; node.city = city; node.site = null; } }
            return node;
        }
        void Update()
        {
            Resolve(); if (!EnabledFor(this) || world == null || !world.Initialized || world.SimulationEnded || Editing()) return;
            if (Input.GetKeyDown(KeyCode.R)) { ToggleDraft(DraftKind.Patrol);UnitContextOrders.ConsumePointer(); return; }
            if (Drafting)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.T)) { CancelDraft(); UnitContextOrders.ConsumePointer(); return; }
                if (Input.GetMouseButtonDown(1)) { CommitDraft(); UnitContextOrders.ConsumePointer(); return; }
                if (Input.GetMouseButtonDown(0) && !OverUI()) { AddDraft(PointAt(Input.mousePosition, MouseWorld())); UnitContextOrders.ConsumePointer(); }
                return;
            }
            if (UnitContextOrders.Consumed || hud == null || hud.ActiveTool != CampaignHUD.Tool.None || !Input.GetMouseButtonDown(0) || OverUI()) return;
            if (TryTruckClick(Input.mousePosition, MouseWorld())) { UnitContextOrders.ConsumePointer(); return; }
            bool friendly = world.units.Any(u => u != null && u.OnMap && u.ownerId == world.currentPlayer && Vector2.Distance(world.setup.mapCamera.WorldToScreenPoint(u.transform.position), Input.mousePosition) < world.setup.settings.selectionPixels);
            if (!friendly) foreach (var mover in selection.SelectedArmies.ToArray()) CancelUnit(mover.GetComponent<BattleUnit>(), false);
        }
        public bool ToggleDraft(DraftKind kind){return Drafting&&CurrentDraft==kind?CommitDraft():BeginDraft(kind);}
        public bool BeginDraft(DraftKind kind=DraftKind.Patrol)
        {
            Resolve(); if (!EnabledFor(this)) return false;
            GetComponent<UnitContextOrders>()?.CancelSplitPreview();draftUnits.Clear(); draft.Clear(); foreach (var m in selection.SelectedArmies) { var u = m.GetComponent<BattleUnit>(); if (u != null && u.CanMove && u.ownerId == world.currentPlayer) draftUnits.Add(u); }
            if (draftUnits.Count == 0) { Feedback("先选择可移动的己方单位，再按 R"); return false; }
            hud.CancelTool();world.GetComponent<LocalCoordinatedOrders>()?.CancelPicking(); world.GetComponent<RoadMarchController>()?.CancelDraft();world.GetComponent<FreeMarchController>()?.CancelDraft();world.GetComponent<GroupMarchController>()?.Cancel();world.GetComponent<LocalTacticalBattle>()?.CancelPointerDraft(); world.GetComponent<UnitContextOrders>()?.CancelEntries(false); world.GetComponent<TacticalTargetOrders>()?.CancelAll(false);
            foreach (var u in draftUnits){GetComponent<LocalCoordinatedOrders>()?.CancelForNewTask(u);CancelUnit(u,true);}
            CurrentDraft=kind;Drafting = true; Feedback(kind==DraftKind.Patrol?"巡逻运输：先点装货设施，再点卸货设施；再次点击巡逻 / R 开始执行":kind==DraftKind.Road?"自动寻路：依次点击路点；再次点击自动寻路 / Q 开始执行":"直线行军：依次点击路点；再次点击直线行军 / E 开始执行"); return true;
        }
        public void AddDraft(Waypoint node) { if (!Drafting || node == null || draft.Count >= 32) return; draft.Add(node); DrawDraft(); }
        void DrawDraft()
        {
            if (preview == null) { var g = new GameObject("R 巡逻路线预览"); g.transform.SetParent(transform, false); preview = g.AddComponent<LineRenderer>(); material = new Material(Shader.Find("Sprites/Default")); preview.sharedMaterial = material; preview.sortingOrder = 27; preview.startWidth = preview.endWidth = .06f; preview.startColor = preview.endColor = StaffMapStyle.Blue; }
            var points = new List<Vector3> { draftUnits[0].transform.position }; points.AddRange(draft.Select(n => n.Position)); if(CurrentDraft==DraftKind.Patrol&&draft.Count>1)points.Add(draft[0].Position); preview.positionCount = points.Count; preview.SetPositions(points.ToArray()); preview.enabled = true;
        }
        public bool CommitDraft()
        {
            if (!Drafting) return false; bool ok = draft.Count > 0;
            if (ok) {ok=false;foreach (var u in draftUnits.ToArray()) {if(CurrentDraft==DraftKind.Patrol){if(StartPatrol(u,draft))ok=true;}else if(StartRoute(u,draft,CurrentDraft==DraftKind.Straight))ok=true;}}
            bool patrol=CurrentDraft==DraftKind.Patrol;CancelDraft(); Feedback(ok ? patrol?"已开始循环巡逻运输；T 停止":"路线已确认并开始执行；T 停止" : "没有有效路线，未执行"); return ok;
        }
        public void CancelDraft() { Drafting = false; draft.Clear(); draftUnits.Clear(); if (preview != null) preview.enabled = false; }
        public bool StartPatrol(BattleUnit unit, IList<Waypoint> nodes)
        { Resolve();if(NetworkTacticalOrders.Forward(world,unit,"patrol",out var forwarded,nodes:nodes))return forwarded;
            if (!EnabledFor(this) || unit == null || unit.ownerId!=NetworkTacticalOrders.Player(world,unit.ownerId) || !unit.CanMove || nodes == null || nodes.Count < 2) return false;
            var copy=nodes.Select(n=>new Waypoint{position=n.position,site=n.site,city=n.city}).ToList();
            if(unit.GetComponent<TransportTruck>()!=null){if(copy.Any(n=>n.city!=null&&n.city.ownerId!=unit.ownerId)){Feedback("运输只能使用己方仓库和据点");return false;}if(!copy.Any(n=>n.site!=null)){var source=copy.FirstOrDefault(n=>n.city!=null);if(source!=null)source.loadStorage=true;}}
            GetComponent<LocalCoordinatedOrders>()?.CancelForNewTask(unit);CancelUnit(unit, true); jobs[unit] = new Job { unit = unit, nodes = copy, index = 0, repeat = true, state = "巡逻" }; status[unit] = "巡逻";unit.GetComponent<TransportTruck>()?.SetLocalAutomatic(true); return true;
        }
        public bool StartRoute(BattleUnit unit,IList<Waypoint> nodes,bool straight){Resolve();if(NetworkTacticalOrders.Forward(world,unit,"route",out var forwarded,nodes:nodes,straight:straight))return forwarded;
            if(!EnabledFor(this)||unit==null||unit.ownerId!=NetworkTacticalOrders.Player(world,unit.ownerId)||!unit.CanMove||nodes==null||nodes.Count==0)return false;
            var route=new List<RoadPoint>{new RoadPoint(unit.transform.position.x,unit.transform.position.y)};var access=unit.GetComponent<ArmyRoadMover>().AllowedTerrain;var from=unit.transform.position;
            foreach(var node in nodes){var to=node.Position;var segment=straight?new List<RoadPoint>{new RoadPoint(from.x,from.y),new RoadPoint(to.x,to.y)}:LocalRoadAStar.Plan(world.setup.freeMarchMap,access,from,to,.2f,100000);if(segment==null||straight&&!world.setup.freeMarchMap.CanTraverse(segment[0],segment[1],access)){Feedback("路线包含不可通行地形");return false;}foreach(var p in segment)RoadNetwork.Append(route,p);from=to;}
            return world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.FreeRoute,playerId=unit.ownerId,unitId=unit.unitId,route=route}).accepted;
        }
        public object SuspendForSupport(BattleUnit unit){if(unit==null||!jobs.TryGetValue(unit,out var job))return null;jobs.Remove(unit);job.moving=false;return job;}
        public bool ResumeAfterSupport(BattleUnit unit,object saved){var job=saved as Job;if(unit==null||!unit.OnMap||job==null||job.unit!=unit||jobs.ContainsKey(unit))return false;job.moving=false;jobs[unit]=job;unit.GetComponent<TransportTruck>()?.SetLocalAutomatic(job.automatic||job.repeat&&unit.GetComponent<TransportTruck>()!=null);status[unit]=job.automatic?"自动运输":"巡逻";return true;}
        public void CancelUnit(BattleUnit unit, bool stop)
        {
            if (unit == null) return; jobs.Remove(unit); status[unit] = "待命"; if (stop) unit.Motor?.Stop(); unit.GetComponent<TransportTruck>()?.SetLocalAutomatic(false); var truck=unit.GetComponent<TransportTruck>();if(truck!=null)cityClicks.Remove(truck);
        }
        public void CancelCommand(BattleCommand command)
        {
            if (command == null) return;
            switch (command.kind) { case BattleCommandKind.StopSelected: case BattleCommandKind.NudgeSelected: if (command.unitIds != null) foreach (var id in command.unitIds) CancelUnit(world.units.FirstOrDefault(u => u != null && u.unitId == id && u.ownerId == command.playerId), false); if (Drafting) CancelDraft(); return;
                case BattleCommandKind.StopMovement: case BattleCommandKind.RoadRoute: case BattleCommandKind.RoadDestination: case BattleCommandKind.FreeRoute: case BattleCommandKind.FreeDestination: case BattleCommandKind.StraightDestination: case BattleCommandKind.EnterPoint: case BattleCommandKind.BoardCarrier: case BattleCommandKind.BuildFortification: CancelUnit(world.units.FirstOrDefault(u => u != null && u.unitId == command.unitId && u.ownerId == command.playerId), false); return; }
        }
        RecoveryPoint Destination(BattleUnit u) { return world.points.Where(p => p != null && p.kind == FacilityKind.RecoveryPoint && !p.isRepairSite && p.ownerId == u.ownerId).OrderBy(p => Vector2.Distance(p.transform.position, u.transform.position)).FirstOrDefault(); }
        ResourceSite Source(BattleUnit u) { return world.campaign.logistics.sites.Where(p => p != null && p.stock > .001f).OrderBy(p => Vector2.Distance(p.transform.position, u.transform.position)).FirstOrDefault(); }
        public bool StartAutomatic(TransportTruck truck)
        {
            Resolve();if(NetworkTacticalOrders.Forward(world,truck==null?null:truck.Unit,"automatic",out var forwarded))return forwarded; if (!EnabledFor(this) || truck == null || !truck.Unit.CanMove) return false;
            var site = Source(truck.Unit); var city = Destination(truck.Unit); if (city == null || site == null && truck.Cargo <= .001f) { Feedback("没有可用资源点或己方城市"); return false; }
            GetComponent<LocalCoordinatedOrders>()?.CancelForNewTask(truck.Unit);CancelUnit(truck.Unit, true); var nodes = new List<Waypoint>(); if (site != null) nodes.Add(new Waypoint { site = site }); nodes.Add(new Waypoint { city = city });
            jobs[truck.Unit] = new Job { unit = truck.Unit, nodes = nodes, index = truck.Cargo >= truck.Capacity - .001f ? nodes.Count - 1 : 0, repeat = true, automatic = true, state = "自动运输" }; truck.SetLocalAutomatic(true); status[truck.Unit] = "自动运输"; return true;
        }
        public void EnsureAiTransports() { if (!EnabledFor(this)) return; foreach (var u in world.units) if (u != null && u.ownerId == BattleFactions.RedPlayerId && u.CanMove && !IsAssigned(u)) { var t = u.GetComponent<TransportTruck>(); if (t != null) StartAutomatic(t); } }
        public bool TryTruckClick(Vector2 screen, Vector3 ground)
        {
            Resolve(); var unit = selection.Selected == null ? null : selection.Selected.GetComponent<BattleUnit>(); var truck = unit == null ? null : unit.GetComponent<TransportTruck>(); if (truck == null || !unit.OnMap) return false;
            var point = PointAt(screen, ground); float nodeDistance=point.site!=null||point.city!=null?Vector2.Distance(screen,world.setup.mapCamera.WorldToScreenPoint(point.Position)):float.MaxValue; if(world.units.Any(u=>u!=null&&u!=unit&&u.OnMap&&u.ownerId==unit.ownerId&&Vector2.Distance(screen,world.setup.mapCamera.WorldToScreenPoint(u.transform.position))<nodeDistance))return false; if(point.site!=null||point.city!=null){world.GetComponent<UnitContextOrders>()?.CancelEntries(false);world.GetComponent<TacticalTargetOrders>()?.CancelAll(false);} if (point.site != null) { CancelUnit(unit, true); StartDelivery(unit,new List<Waypoint>{point}); return true; }
            if (point.city == null) return false; var city = point.city;
            if (truck.Cargo > .001f && (!cityClicks.TryGetValue(truck, out var clicked) || clicked != city))
            {
                if (city.ownerId != unit.ownerId) { world.GetComponent<UnitContextOrders>()?.RejectMovement("只能向己方城市卸货"); return true; }
                CancelUnit(unit, true); cityClicks[truck] = city; StartDelivery(unit,new List<Waypoint>{point}); return true;
            }
            CancelUnit(unit, true); world.GetComponent<UnitContextOrders>().QueueEntry(unit, city, null); return true;
        }
        public bool StartDelivery(BattleUnit unit,IList<Waypoint> nodes){Resolve();if(NetworkTacticalOrders.Forward(world,unit,"delivery",out var forwarded,nodes:nodes))return forwarded;if(unit==null||!unit.CanMove||unit.GetComponent<TransportTruck>()==null||nodes==null||nodes.Count!=1||unit.ownerId!=NetworkTacticalOrders.Player(world,unit.ownerId))return false;CancelUnit(unit,true);jobs[unit]=new Job{unit=unit,nodes=nodes.ToList(),state="运输中"};return true;}
        static float DockRange(BattleUnit u) { return Mathf.Max(.25f, UnitRanges.Interaction(u)); }
        bool At(Job job, Waypoint node)
        {
            var truck = job.unit.GetComponent<TransportTruck>();
            if (truck != null && node.site != null) { var area = node.site.Area; area.xMin -= .08f; area.xMax += .08f; area.yMin -= .08f; area.yMax += .08f; return area.Contains(job.unit.transform.position); }
            return Vector2.Distance(job.unit.transform.position, node.Position) <= (truck != null && node.city != null ? DockRange(job.unit) : .08f);
        }
        bool Move(Job job, Waypoint node)
        {
            var unit = job.unit; var map = world.setup.freeMarchMap; var access = unit.GetComponent<ArmyRoadMover>().AllowedTerrain;
            var candidates = new List<Vector3> { node.Position };
            if (unit.GetComponent<TransportTruck>() != null && node.site != null) { var a = node.site.Area; for (int y = 1; y <= 3; y++) for (int x = 1; x <= 3; x++) candidates.Add(new Vector3(a.xMin + a.width * x / 4, a.yMin + a.height * y / 4)); }
            foreach (var target in candidates.OrderBy(p => Vector2.Distance(p, unit.transform.position)))
            {
                var point = new RoadPoint(target.x, target.y); if (!map.CanTraverse(point, point, access)) continue;
                var path = LocalRoadAStar.Plan(map, access, unit.transform.position, target, Vector2.Distance(unit.transform.position, target) > 12 ? .4f : .2f, 30000);
                if (path == null || !unit.GetComponent<ArmyRoadMover>().CommitAutomaticRoute(path)) continue;
                job.moving = true; job.revision = unit.Motor.ArrowOrderRevision; job.state = node.site != null ? "前往资源点" : node.city != null ? "前往城市" : "巡逻行军"; return true;
            }
            return false;
        }
        void Load(TransportTruck truck, ResourceSite site, float dt) { float n = Mathf.Max(0, Mathf.Min(site.stock, Mathf.Min(truck.Capacity - truck.Cargo, dt * ResourceEconomy.LoadPerSecond))); site.stock -= n; truck.SetCargo(truck.Cargo + n); }
        void LoadStorage(TransportTruck truck,RecoveryPoint point,float dt){float n=Mathf.Max(0,Mathf.Min(point.resourceStock,Mathf.Min(truck.Capacity-truck.Cargo,dt*ResourceEconomy.LoadPerSecond)));point.resourceStock-=n;truck.SetCargo(truck.Cargo+n);}
        void Unload(TransportTruck truck, RecoveryPoint city, float dt) { float n = Mathf.Max(0,Mathf.Min(truck.Cargo, dt * ResourceEconomy.UnloadPerSecond)); city.resourceStock += n; truck.SetCargo(truck.Cargo - n); }
        void Advance(Job job)
        {
            job.index++; job.moving = false; if (job.index >= job.nodes.Count) { if (job.repeat) job.index = 0; else { jobs.Remove(job.unit); job.unit.GetComponent<TransportTruck>()?.SetLocalAutomatic(false); status[job.unit] = "完成"; } }
        }
        public void Tick(float dt)
        {
            Resolve(); if (!NetworkTacticalOrders.Authority || !EnabledFor(this) || world == null || !world.Initialized || world.SimulationEnded) return; dt = Mathf.Max(0, dt);
            var processed=new HashSet<BattleUnit>(jobs.Keys);foreach (var job in jobs.Values.ToArray())
            {
                var unit = job.unit; if (unit == null || !unit.OnMap) { CancelUnit(unit, false); continue; }
                if (!unit.CanMove) { status[unit] = "等待单位恢复可移动状态"; continue; }
                if (job.moving && unit.Motor.ArrowOrderRevision != job.revision) { CancelUnit(unit, false); continue; }
                var node = job.nodes[job.index]; var truck = unit.GetComponent<TransportTruck>();
                if (truck != null && node.city != null && node.city.ownerId != unit.ownerId) { var replacement = job.automatic ? Destination(unit) : null; if (replacement == null) { CancelUnit(unit, true); status[unit] = "目标城市不可用，保留货物"; continue; } node.city = replacement; job.moving = false; unit.Motor.Stop(); }
                if (!At(job, node)) { if (unit.Motor.IsMoving && job.moving) continue; if (!Move(job, node)) { CancelUnit(unit, true); status[unit] = "路线不可通行，已停止"; } else status[unit] = job.state; continue; }
                if (unit.Motor.IsMoving) unit.Motor.Stop(); job.moving = false;
                if (truck != null && node.site != null) { Load(truck, node.site, dt); status[unit] = "装货中"; if (truck.Cargo < truck.Capacity - .001f && node.site.stock > .001f) continue; if (job.automatic && node.site.stock <= .001f) { var next = Source(unit); if (next != null) { node.site = next; if(truck.Cargo < truck.Capacity-.001f)continue; } else if (truck.Cargo <= .001f) { CancelUnit(unit, false); status[unit] = "资源已耗尽"; continue; } } }
                else if (truck != null && node.city != null) {if(!node.city.isActiveAndEnabled){CancelUnit(unit,true);status[unit]="运输设施已失效，保留货物";continue;}if(node.loadStorage){LoadStorage(truck,node.city,dt);status[unit]="装货中";if(truck.Cargo<truck.Capacity-.001f){if(node.city.resourceStock<=.001f&&truck.Cargo<=.001f)status[unit]="等待来源补给";if(node.city.resourceStock>0||truck.Cargo<=.001f)continue;}}else{ Unload(truck, node.city, dt); status[unit] = "卸货中"; if (truck.Cargo > .001f) continue; if(job.automatic && Source(unit)==null){CancelUnit(unit,false);status[unit]="资源已耗尽";continue;}} }
                Advance(job);
            }
            foreach (var unit in world.units)
            {
                var truck = unit == null ? null : unit.GetComponent<TransportTruck>(); if (truck == null || !unit.Alive || processed.Contains(unit)) continue;
                if (!unit.OnMap) { if (unit.Point != null && unit.Point.ownerId == unit.ownerId) Unload(truck, unit.Point, dt); continue; }
                if (unit.Motor.IsMoving) continue; var city = world.points.FirstOrDefault(p => p != null && p.ownerId == unit.ownerId && p.kind == FacilityKind.RecoveryPoint && Vector2.Distance(p.transform.position, unit.transform.position) <= DockRange(unit));
                if (city != null && truck.Cargo > .001f) { Unload(truck, city, dt); continue; }
                var pile=world.campaign.depots.FirstOrDefault(p=>p!=null&&!p.Removed&&p.resourcePile&&p.stock>0&&Vector2.Distance(p.transform.position,unit.transform.position)<=DockRange(unit));if(pile!=null){float n=Mathf.Max(0,Mathf.Min(pile.stock,Mathf.Min(truck.Capacity-truck.Cargo,dt*ResourceEconomy.LoadPerSecond)));pile.stock-=n;truck.SetCargo(truck.Cargo+n);if(pile.stock<=.001f)pile.Remove();continue;}
                var site = world.campaign.logistics.sites.FirstOrDefault(s => s != null && s.stock > 0 && s.Area.Contains(unit.transform.position)); if (site != null) Load(truck, site, dt);
            }
        }
        void OnDestroy() { if (preview != null) Destroy(preview.gameObject); if (material != null) Destroy(material); }
    }
}
