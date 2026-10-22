using UnityEngine;
namespace CampaignDemo { public sealed class StaffMapPaper:MonoBehaviour {
void Start(){var view=GetComponent<StaffMapCartography>();if(view==null)view=gameObject.AddComponent<StaffMapCartography>();view.Invalidate();}
} }