using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Continuous deterministic attrition. RK4 stages use simultaneous state snapshots.
    public static class CampaignCombatSolver
    {
        sealed class Target
        {public BattleUnit unit;public Fortification fort;public ResourceCollector collector;public Vector3 position;public string id,owner;public float Value {get{return unit!=null?unit.Personnel:fort!=null?fort.Health:collector.Health;}}}
        sealed class Link {public int target;public float distance;}
        public static void Resolve(CampaignRuntime c,float duration)
        {
            var w=c.world;var r=c.rules;var nodes=new List<Target>();
            foreach(var u in w.units)if(u!=null&&u.OnMap)nodes.Add(new Target{unit=u,position=u.transform.position,id=u.unitId,owner=u.ownerId});
            if(c.combined!=null)foreach(var f in c.combined.fortifications)if(f!=null&&!f.Destroyed)nodes.Add(new Target{fort=f,position=f.transform.position,id="fort:"+f.GetInstanceID(),owner=f.ownerId});
            if(c.logistics!=null)foreach(var collector in c.logistics.collectors)if(collector!=null&&collector.Alive)nodes.Add(new Target{collector=collector,position=collector.transform.position,id=collector.collectorId,owner=collector.ownerId});
            nodes.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));
            int n=nodes.Count;if(n==0||duration<=0)return;if(r.integrationStep<=0||float.IsNaN(r.integrationStep)||float.IsInfinity(r.integrationStep))throw new ArgumentException("Integration step must be finite and positive");var links=new List<Link>[n];var attackBuff=new float[n];var incoming=new float[n];var flanks=new float[n,n];var directions=new List<float>[n];
            for(int i=0;i<n;i++){links[i]=new List<Link>();directions[i]=new List<float>();}
            for(int i=0;i<n;i++)
            {
                var attacker=nodes[i].unit;if(attacker==null||!attacker.CanFire||(!c.CanAttack(attacker)&&attacker.GetComponent<CampaignUnit>().fortTarget==null))continue;
                float range=Mathf.Min(attacker.combatDefinition.attackRadius,attacker.combatDefinition.visionRadius);
                for(int j=0;j<n;j++)
                {
                    var t=nodes[j];var forced=attacker.GetComponent<CampaignUnit>().fortTarget;if(forced!=null&&t.fort!=forced)continue;if(i==j||(t.owner==nodes[i].owner&&(forced==null||forced!=t.fort)))continue;float distance=t.fort!=null?t.fort.DistanceTo(nodes[i].position):Vector2.Distance(nodes[i].position,t.position);if(distance>range)continue;
                    bool visible=t.unit!=null?w.IsVisible(attacker.ownerId,t.unit):!w.fogEnabled||w.VisibleAt(attacker.ownerId,t.fort==null?t.position:t.fort.ClosestPoint(nodes[i].position));if(!visible)continue;
                    // Fortifications in front of their garrison take the attack first.
                    
                    links[i].Add(new Link{target=j,distance=distance});
                    if(t.unit!=null){var state=t.unit.GetComponent<CampaignUnit>();flanks[i,j]=state==null?1:state.FlankFrom(nodes[i].position,r);incoming[j]=state!=null&&state.Moving?r.movingIncomingMultiplier:1;directions[j].Add(Mathf.Atan2(nodes[i].position.y-t.position.y,nodes[i].position.x-t.position.x)*Mathf.Rad2Deg);}
                }
                links[i].Sort((a,b)=>{int v=a.distance.CompareTo(b.distance);return v!=0?v:string.CompareOrdinal(nodes[a.target].id,nodes[b.target].id);});
                attackBuff[i]=1;
            }
            var encircle=new float[n];for(int j=0;j<n;j++){directions[j].Sort();var chosen=new List<float>();foreach(float angle in directions[j]){bool separate=true;foreach(float other in chosen)if(Mathf.Abs(Mathf.DeltaAngle(angle,other))<r.directionSeparationDegrees){separate=false;break;}if(separate)chosen.Add(angle);}encircle[j]=Mathf.Min(r.encirclementCap,1+Mathf.Max(0,chosen.Count-1)*r.encirclementIncrement);}
            var x=new double[n];var initial=new double[n];for(int i=0;i<n;i++)x[i]=initial[i]=nodes[i].Value;
            var k1=new double[n];var k2=new double[n];var k3=new double[n];var k4=new double[n];var temp=new double[n];
            Action<double[],double[]> derivative=(values,result)=>
            {
                Array.Clear(result,0,n);
                for(int i=0;i<n;i++)
                {
                    if(values[i]<=0||links[i].Count==0)continue;Func<int,bool> eligible=j=>{if(values[j]<=0)return false;if(nodes[j].unit!=null){var cover=c.CoveringFort(nodes[j].unit);if(cover!=null)foreach(var other in links[i])if(nodes[other.target].fort==cover&&values[other.target]>0)return false;}return true;};double total=0,weight=1;foreach(var link in links[i])if(eligible(link.target)){total+=weight;weight*=r.distanceDecay;}if(total<=0)continue;
                    float buff=1;var unit=nodes[i].unit;var fort=unit==null?null:c.ProtectingFort(unit);if(fort!=null)for(int p=0;p<n;p++)if(nodes[p].fort==fort&&values[p]>0){buff=r.fortAttackMultiplier*Mathf.Pow(r.fortLevelAttackFactor,fort.level);break;}
                    double output=r.lossPerStrengthPerSecond*values[i]*buff;weight=1;
                    foreach(var link in links[i]){int j=link.target;if(!eligible(j))continue;double amount=output*weight/total;weight*=r.distanceDecay;if(nodes[j].unit!=null){amount*=incoming[j]*flanks[i,j]*encircle[j];if(amount>0)nodes[j].unit.GetComponent<CampaignUnit>().ObserveAttack(unit);}if(unit.combatDefinition.role==UnitRole.Engineer)amount*=nodes[j].fort!=null?r.engineerFortDamageMultiplier:r.engineerOrdinaryAttackMultiplier;result[j]-=amount;}
                }
            };
            double left=duration;while(left>0)
            {
                double h=Math.Min(left,r.integrationStep);derivative(x,k1);for(int i=0;i<n;i++)temp[i]=Math.Max(0,x[i]+h*k1[i]/2);derivative(temp,k2);for(int i=0;i<n;i++)temp[i]=Math.Max(0,x[i]+h*k2[i]/2);derivative(temp,k3);for(int i=0;i<n;i++)temp[i]=Math.Max(0,x[i]+h*k3[i]);derivative(temp,k4);for(int i=0;i<n;i++)x[i]=Math.Max(0,x[i]+h*(k1[i]+2*k2[i]+2*k3[i]+k4[i])/6);left-=h;
            }
            // Notify visible firing before applying terminal damage; no effect can control the solver.
            for(int i=0;i<n;i++)if(links[i].Count>0&&nodes[i].unit!=null&&initial[i]>0)
            {
                var attacker=nodes[i].unit;bool fired=false;foreach(var link in links[i])if(initial[link.target]-x[link.target]>0){var t=nodes[link.target];if(t.unit!=null){w.Intel.Identify(attacker.ownerId,t.unit);w.Intel.Identify(t.owner,attacker);}if(w.revealForestOnAttack)attacker.RevealForest();w.NotifyCombatEffect(CombatEffectEvent.Direct(attacker,t.id,t.owner,t.fort==null?t.position:t.fort.ClosestPoint(nodes[i].position),t.collector!=null));fired=true;}
                if(fired)attacker.Engage();
            }
            foreach(var u in w.units)if(u!=null&&u.State==ArmyBattleState.Engaged)u.ReleaseCombat();
            for(int i=0;i<n;i++){float loss=(float)(initial[i]-x[i]);if(loss<=0)continue;if(nodes[i].unit!=null)nodes[i].unit.Damage(loss);else if(nodes[i].fort!=null)nodes[i].fort.Damage(loss);else nodes[i].collector.Damage(loss);}
        }
    }
}






