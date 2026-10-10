using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Build the complete impact snapshot before applying casualties or destroying cover.
    public static class CampaignShellResolver
    {
        sealed class Victim { public BattleUnit unit; public float strength, healthPerStrength; public bool covered; public float loss; }
        sealed class Structure { public Fortification fort; public ResourceCollector collector; public float weight, loss; }
        public static float EquivalentStrength(CampaignRuntime campaign,BattleUnit unit)
        { return campaign.Vehicle(unit)?campaign.rules.At(campaign.rules.transportEquivalents,unit.combatDefinition.echelon)*unit.Fraction:unit.Personnel; }
        public static float ExposedLength(Fortification fort,Vector3 center,float blastRadius)
        {
            float radius=Mathf.Max(0,blastRadius)+fort.radius;
            if(!fort.IsLine)return Vector2.Distance(fort.transform.position,center)<=radius?2*fort.radius:0;
            float total=0;
            for(int i=1;i<fort.linePoints.Length;i++)
            {
                Vector2 a=fort.linePoints[i-1],d=(Vector2)fort.linePoints[i]-a,relative=a-(Vector2)center;
                float length=d.magnitude;if(length<=Mathf.Epsilon)continue;
                float aa=d.sqrMagnitude,bb=2*Vector2.Dot(relative,d),cc=relative.sqrMagnitude-radius*radius;
                float discriminant=bb*bb-4*aa*cc;if(discriminant<=0)continue;
                float root=Mathf.Sqrt(discriminant),from=Mathf.Clamp01((-bb-root)/(2*aa)),to=Mathf.Clamp01((-bb+root)/(2*aa));
                total+=Mathf.Max(0,to-from)*length;
            }
            return total;
        }
        public static void Resolve(CampaignRuntime campaign,Vector3 center,float radius,float budget,string sourceId)
        {
            budget=Mathf.Max(0,budget);radius=Mathf.Max(0,radius);if(budget<=0)return;
            var structures=new List<Structure>();var struck=new HashSet<Fortification>();float totalWeight=0;
            if(campaign.combined!=null)foreach(var fort in campaign.combined.fortifications)
            {
                if(fort==null||fort.Destroyed||!struck.Add(fort))continue;
                float weight=ExposedLength(fort,center,radius);if(weight<=0){struck.Remove(fort);continue;}
                structures.Add(new Structure{fort=fort,weight=weight});totalWeight+=weight;
            }
            if(campaign.rules.shellCollectorsShareStructureBudget&&campaign.logistics!=null)
            {
                var seen=new HashSet<ResourceCollector>();foreach(var collector in campaign.logistics.collectors)
                {
                    if(collector==null||!collector.Alive||!seen.Add(collector)||Vector2.Distance(collector.transform.position,center)>radius)continue;
                    float weight=Mathf.Max(Mathf.Epsilon,Mathf.Max(collector.settings.footprint.x,collector.settings.footprint.y));
                    structures.Add(new Structure{collector=collector,weight=weight});totalWeight+=weight;
                }
            }
            var victims=new List<Victim>();var unique=new HashSet<BattleUnit>();float total=0,protectedStrength=0;BattleUnit source=null;
            foreach(var unit in campaign.world.units)
            {
                if(unit!=null&&unit.unitId==sourceId)source=unit;
                if(unit==null||!unit.OnMap||!unique.Add(unit)||Vector2.Distance(unit.transform.position,center)>radius)continue;
                float strength=EquivalentStrength(campaign,unit);if(strength<=0)continue;
                var fort=campaign.CoveringFort(unit);bool covered=fort!=null&&struck.Contains(fort);
                victims.Add(new Victim{unit=unit,strength=strength,healthPerStrength=unit.Personnel/strength,covered=covered});total+=strength;if(covered)protectedStrength+=strength;
            }
            float share=Mathf.Clamp01(campaign.rules.shellFortDamageShare);
            float fraction=total>0?Mathf.Min(budget/total,Mathf.Clamp01(campaign.rules.shellMaximumLossFraction)):0;
            foreach(var victim in victims)victim.loss=victim.strength*fraction*(victim.covered?1-share:1)*victim.healthPerStrength;
            float structureBudget=total>0?budget*share*protectedStrength/total:budget*share;
            foreach(var structure in structures)structure.loss=totalWeight>0?structureBudget*structure.weight/totalWeight:0;
            foreach(var victim in victims){if(victim.loss>0&&source!=null)victim.unit.GetComponent<CampaignUnit>()?.ObserveAttack(source);victim.unit.Damage(victim.loss);}
            foreach(var structure in structures){if(structure.fort!=null)structure.fort.Damage(structure.loss);else structure.collector.Damage(structure.loss);}
            // Compatibility option only. The new campaign defaults share one material budget.
            if(!campaign.rules.shellCollectorsShareStructureBudget&&campaign.logistics!=null)foreach(var collector in campaign.logistics.collectors)
                if(collector!=null&&collector.Alive&&Vector2.Distance(collector.transform.position,center)<=radius)collector.Damage(budget);
        }
    }
}
