using UnityEditor;
using UnityEngine;
using CampaignDemo;
[CustomEditor(typeof(CityProductionRules))]
public sealed class CityProductionRulesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        string[] names={"infantryPrefab","infantrySquadCost","infantrySquadSeconds","infantryCostScale","infantryTimeScale","infantryRequiresFactory","maximumQueuedPerCity"};
        string[] labels={"普通步兵预制体","一个班制造费用","一个班制造秒数","每升一级费用倍率","每升一级时间倍率","造兵必须有兵工厂","每城生产队列上限"};
        EditorGUILayout.HelpBox("玩家和AI共用这套规则。修改价格和时间不会改变已经付款的订单。",MessageType.Info);
        for(int i=0;i<names.Length;i++)EditorGUILayout.PropertyField(serializedObject.FindProperty(names[i]),new GUIContent(labels[i]));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("additionalUnits"),new GUIContent("其他兵种生产目录（可调测试值）"),true);serializedObject.ApplyModifiedProperties();
    }
}
