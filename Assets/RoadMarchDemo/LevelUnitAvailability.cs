using UnityEngine;
namespace CampaignDemo
{
    [DisallowMultipleComponent]
    public sealed class LevelUnitAvailability : MonoBehaviour
    {
        [Header("本关允许生产的兵种；默认全部开启")]
        [InspectorName("普通步兵")] public bool infantry=true;
        [InspectorName("侦察兵")] public bool scout=true;
        [InspectorName("炮兵")] public bool artillery=true;
        [InspectorName("工兵")] public bool engineer=true;
        [InspectorName("坦克")] public bool tank=true;
        [InspectorName("运兵车")] public bool personnelCarrier=true;
        [InspectorName("资源车")] public bool logistics=true;
        [InspectorName("山地步兵")] public bool mountainInfantry=true;
        [InspectorName("火箭兵")] public bool rocketArtillery=true;
        public bool Allows(UnitRole role)
        {switch(role){case UnitRole.Infantry:return infantry;case UnitRole.Scout:return scout;case UnitRole.Artillery:return artillery;case UnitRole.RocketArtillery:return rocketArtillery;case UnitRole.Radar:return false;case UnitRole.Engineer:return engineer;case UnitRole.Tank:return tank;case UnitRole.PersonnelCarrier:return personnelCarrier;case UnitRole.Logistics:return logistics;case UnitRole.MountainInfantry:return mountainInfantry;default:return false;}}
        public static bool Allowed(BattleWorld world,UnitRole role){if(role==UnitRole.Radar)return false;var settings=world==null?null:world.GetComponent<LevelUnitAvailability>();return settings==null||!settings.isActiveAndEnabled||settings.Allows(role);}
        public static string Name(UnitRole role){return new[]{"步兵","侦察兵","炮兵","工兵","坦克","运兵车","资源车","山地步兵","火箭兵","雷达"}[(int)role];}
    }
}
