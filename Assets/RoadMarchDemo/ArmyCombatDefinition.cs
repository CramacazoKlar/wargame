using UnityEngine;
namespace CampaignDemo
{
    public enum UnitRole { Infantry, Scout, Artillery, Engineer, Tank, PersonnelCarrier, Logistics, MountainInfantry }
    public enum ArmyEchelon { Squad, Platoon, Company, Battalion, Regiment, Brigade, Division, Corps }
    [CreateAssetMenu(menuName="Campaign Demo/Army Combat Type",fileName="ArmyCombatType")]
    public sealed class ArmyCombatDefinition : ScriptableObject
    {
        [Tooltip("该兵种是否可以在森林范围内藏匿。")] public bool canHideInForest=true;
        public string unitType="Infantry";
        public UnitRole role=UnitRole.Infantry;
        public ArmyEchelon echelon=ArmyEchelon.Regiment;
        [Min(1)] public int squadSize=7;
        [Range(2,4)] public int echelonRatio=3;
        [Min(0)] public float fullStrengthAttack=15;
        [Min(0)] public float defense=20;
        [Header("新版普通交火；满编班为基准，炮兵仍由玩家发射")]
        [Min(.01f)] public float squadHealth=7;
        [Range(0,1)] public float incomingDamageFactor=1;
        [Min(.01f)] public float attackIntervalSeconds=1;
        [Min(0)] public float squadShotDamage=.14f;
        [Min(.01f)] public float visionRadius=3.5f;
        [Min(.01f)] public float attackRadius=1.1f;
        public int FullPersonnel { get { return CombatMath.FullPersonnel(squadSize,echelonRatio,(int)echelon); } }
    }
    public static class CombatMath
    {
        public static int FullPersonnel(int squad,int ratio,int echelon)
        { long value=System.Math.Max(1,squad);for(int i=0;i<System.Math.Max(0,System.Math.Min(7,echelon));i++)value=System.Math.Min(int.MaxValue,value*System.Math.Max(2,ratio));return (int)value; }
        public static float Strength(float fraction)
        { return fraction>=.75f?1:fraction>=.5f?.75f:fraction>=.25f?.5f:fraction>0?.25f:0; }
        public static float Damage(float attack,float fraction,float defense,float seconds)
        { return Mathf.Max(0,attack)*Strength(fraction)*100/(100+Mathf.Max(0,defense))*Mathf.Max(0,seconds); }
    }
}



