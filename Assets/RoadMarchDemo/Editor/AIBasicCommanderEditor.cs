using UnityEditor;
using UnityEngine;
using CampaignDemo;

[CustomEditor(typeof(AIBasicCommander))]
public sealed class AIBasicCommanderEditor : Editor
{
    bool parameters,manufacture,cheats,pending;
    void Field(SerializedObject data,string name,string label){EditorGUILayout.PropertyField(data.FindProperty(name),new GUIContent(label),true);}
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Field(serializedObject,"controller","红方总控制器");
        Field(serializedObject,"profile","能力模板");
        serializedObject.ApplyModifiedProperties();
        var commander=(AIBasicCommander)target;
        if(commander.profile==null){EditorGUILayout.HelpBox("请先指定能力模板。",MessageType.Info);return;}
        var settings=new SerializedObject(commander.profile);settings.Update();
        EditorGUILayout.Space();EditorGUILayout.LabelField("技能选择",EditorStyles.boldLabel);
        Field(settings,"attackCities","正面集中进攻");Field(settings,"recover","回城恢复");
        Field(settings,"produceResources","资源生产");Field(settings,"recruitInfantry","自动造普通步兵");Field(settings,"transportResources","补给运输");
        EditorGUILayout.HelpBox("勾选表示允许使用。关闭进攻会让进攻队撤回；关闭造兵会取消自动订单并退回未完成费用。配置保存在能力模板里。",MessageType.Info);
        parameters=EditorGUILayout.Foldout(parameters,"进攻、留守与恢复参数",true);
        if(parameters)
        {
            Field(settings,"defenseRatio","最低留守比例");Field(settings,"minimumCityDefense","每座城市最低留守（班当量）");
            Field(settings,"minimumAttackStrength","最低出击兵力（班当量）");Field(settings,"deployHealth","出击健康度");
            Field(settings,"retreatHealth","单位撤回阈值");Field(settings,"recoveryHealth","恢复完成阈值");Field(settings,"squadRetreatRatio","整队撤回阈值");
            Field(settings,"preferRoad","优先公路");Field(settings,"allowGroundFallback","允许合法地面绕行");
            Field(settings,"gatheringSeconds","集结等待时间");Field(settings,"targetRetrySeconds","失败目标重试间隔");
        }
        manufacture=EditorGUILayout.Foldout(manufacture,"造兵与资源分配",true);
        if(manufacture)
        {
            Field(settings,"targetAttackStrength","期望进攻兵力（班当量）");Field(settings,"maximumArmyStrength","总兵力上限（班当量）");
            Field(settings,"maximumProductionJobs","同时造兵上限");Field(settings,"recruitmentEchelon","制造编制");
            Field(settings,"productionReserve","每城最低资源储备");
            EditorGUILayout.HelpBox("制造费用、时间、预制体及兵工厂要求由城市生产规则统一管理，玩家和AI使用同一套规则。",MessageType.Info);
            var producer=commander.GetComponent<InfantryProduction>();
            if(producer!=null&&producer.rules!=null){var rules=new SerializedObject(producer.rules);rules.Update();Field(rules,"infantryPrefab","普通步兵预制体");Field(rules,"infantryRequiresFactory","造兵必须有兵工厂");Field(rules,"infantrySquadCost","一个班制造费用");Field(rules,"infantrySquadSeconds","一个班制造时间");Field(rules,"infantryCostScale","每升一级费用倍率");Field(rules,"infantryTimeScale","每升一级时间倍率");Field(rules,"maximumQueuedPerCity","每城队列上限");rules.ApplyModifiedProperties();}
            Field(settings,"expectedLossRatio","预计进攻损失比例");Field(settings,"maximumCollectors","采集器数量上限");
            Field(settings,"desiredCityStock","城市期望库存");Field(settings,"economyInterval","资源调度间隔");
        }
        cheats=EditorGUILayout.Foldout(cheats,"独立作弊设置",true);
        if(cheats)
        {
            Field(settings,"enableCheats","启用作弊");
            using(new EditorGUI.DisabledScope(!settings.FindProperty("enableCheats").boolValue))
            {Field(settings,"resourceIncomeMultiplier","资源采集倍率");Field(settings,"productionSpeedMultiplier","制造速度倍率");Field(settings,"omniscientEnemyIntel","全图敌军情报");}
            EditorGUILayout.HelpBox("默认关闭。额外优势只给绝对红方AI；切到红方手动控制时不生效。",MessageType.Info);
        }
        pending=EditorGUILayout.Foldout(pending,"后续战术（待实现）",true);
        if(pending)using(new EditorGUI.DisabledScope(true))
        {
            foreach(var label in new[]{"城市回援","预备队增援","侧翼进攻","正面牵制、侧面突击","绕后袭扰","攻击补给线","受损轮换","补给护送"})EditorGUILayout.Toggle(label+"（待实现）",false);
        }
        settings.ApplyModifiedProperties();
        if((commander.Production==null||!commander.Production.HasPrefab)&&commander.profile.recruitInfantry)EditorGUILayout.HelpBox("自动造兵需要普通步兵预制体，缺少时不会扣钱或制造。",MessageType.Warning);
        if(GUILayout.Button("保存能力模板")){EditorUtility.SetDirty(commander.profile);AssetDatabase.SaveAssets();}
        if(Application.isPlaying)
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("当前任务",commander.Status);
            EditorGUILayout.LabelField("资源调度",commander.EconomyStatus??"");
            EditorGUILayout.LabelField("期望总兵力",commander.DesiredArmyStrength.ToString("0.0"));
            EditorGUILayout.LabelField("在造兵力",commander.Production==null?"0":commander.Production.PendingStrength(BattleFactions.RedPlayerId).ToString("0.0"));
            Repaint();
        }
    }
}
