using System.Text;
using UnityEngine;
namespace CampaignDemo
{
    // Presentation formatting only; the information source returns numbers and permissions, never a layout.
    public static class BattleTextFormatter
    {
        public static string Echelon(ArmyEchelon e){return new[]{"班","排","连","营","团","旅","师","军"}[(int)e];}
        public static string State(ArmyBattleState state){return new[]{"地图行动","交战","逃跑","进入据点","驻点","恢复中","出点","被消灭"}[(int)state];}
        public static string Overview(PlayerOverviewInfo s)
        {if(s==null)return "";return "我方区域 "+s.ownAreaPercent.ToString("0.0")+"%   敌方区域 "+s.enemyAreaPercent.ToString("0.0")+"%   中立 "+s.neutralAreaPercent.ToString("0.0")+"%   敌方据点 "+s.enemyCityCount+"\n资源 "+s.TotalResources.ToString("0")+"   部队 "+s.unitCount+"   人数 "+Mathf.CeilToInt(s.personnel)+"   编制 "+Mathf.CeilToInt(s.nominalPersonnel)+" / "+s.personnelLimit+"   据点 "+s.cityCount+"   兵工厂 "+s.factoryCount;}
        public static string Army(ArmyInfo s)
        {
            if(s==null)return "没有可查看的军队信息";
            if(!s.own)return s.known?"敌方部队\n类型："+s.unitType+"\n建制："+Echelon(s.echelon.Value)+"\n人数："+Mathf.CeilToInt(s.personnelPercent.Value)+"%\n\n内部信息不可查看":"未知敌军\n\n尚未交战，类型未知";
            if(s.isPersonnelCarrier)return s.designation+"\n类型：运兵车\n状态："+State(s.state.Value)+"\n蓝圈：视野范围\n绿圈：上车范围\n点击车内部队按钮立即下车";
            var text=new StringBuilder(s.designation);text.Append("\n类型：").Append(s.unitType).Append("\n建制：").Append(Echelon(s.echelon.Value)).Append("\n人数：").Append(Mathf.CeilToInt(s.personnel.Value)).Append(" / ").Append(s.capacity.Value).Append("（").Append(Mathf.CeilToInt(s.personnelPercent.Value)).Append("%）\n战力发挥：").Append(Mathf.RoundToInt(s.strengthPercent.Value)).Append("%\n状态：").Append(State(s.state.Value));
            if(s.hidden==true)text.Append("\n森林状态：").Append(s.ambushEnabled==true?"伏击待命":"藏匿，禁止自动开火");if(s.pointName!=null)text.Append("\n据点：").Append(s.pointName);
            if(s.state==ArmyBattleState.Repairing)text.Append("\n恢复剩余：").Append(s.remainingSeconds.Value.ToString("0.0")).Append(" 秒");else if(s.state==ArmyBattleState.EnteringPoint||s.state==ArmyBattleState.ExitingPoint||s.state==ArmyBattleState.Escaping)text.Append("\n剩余：").Append(s.remainingSeconds.Value.ToString("0.0")).Append(" 秒");return text.ToString();
        }
        public static string Facility(FacilityInfo s)
        {
            if(s==null)return "没有可查看的据点信息";string text=s.name+"\n类型："+(s.kind==FacilityKind.RecoveryPoint?"占领 / 恢复据点":s.kind==FacilityKind.Factory?"兵工厂":"资源点")+"\n归属："+(s.observed?(string.IsNullOrEmpty(s.ownerId)?"中立":s.ownerId):"未知");if(!s.own)return text;text+="\n物资："+s.stock.Value.ToString("0")+"\n收纳上限："+s.capacity.Value+"\n内部部队不能攻击或防守";if(s.capturePercent.HasValue)text+="\n占领进度："+Mathf.RoundToInt(s.capturePercent.Value)+"%";return text;
        }
    }
}

