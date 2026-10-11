using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Discrete ordinary attacks. Same-time shots read a shared pre-damage snapshot.
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
            int n=nodes.Count;if(n==0||duration<=0)return;var links=new List<Link>[n];var attackBuff=new float[n];var incoming=new float[n];var flanks=new float[n,n];var directions=new List<float>[n];
            for(int i=0;i<n;i++){links[i]=new List<Link>();directions[i]=new List<float>();}
            for(int i=0;i<n;i++)
            {
                var attacker=nodes[i].unit;if(attacker==null||!attacker.CanFire||(!c.CanAttack(attacker)&&attacker.GetComponent<CampaignUnit>().fortTarget==null))continue;
                float range=attacker.combatDefinition.attackRadius;
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
            // Resolve shots at their actual cooldown times. Simultaneous shots read one shared snapshot.
            var clocks=new double[n];var shotLoss=new double[n];var firedLinks=new bool[n,n];
            for(int i=0;i<n;i++)if(nodes[i].unit!=null)clocks[i]=Math.Max(0,nodes[i].unit.GetComponent<CampaignUnit>().AttackRemaining);
            double elapsed=0;
            while(elapsed<duration-0.0000001){
                double wait=double.PositiveInfinity;
                for(int i=0;i<n;i++)if(x[i]>0&&nodes[i].unit!=null&&links[i].Count>0&&nodes[i].unit.combatDefinition.squadShotDamage>0){bool any=false;foreach(var link in links[i])if(x[link.target]>0){any=true;break;}if(any)wait=Math.Min(wait,clocks[i]);}
                double available=duration-elapsed;
                if(double.IsPositiveInfinity(wait)||wait>=available-0.0000001){for(int i=0;i<n;i++)clocks[i]=Math.Max(0,clocks[i]-available);break;}
                for(int i=0;i<n;i++)clocks[i]=Math.Max(0,clocks[i]-wait);elapsed+=wait;
                Array.Clear(shotLoss,0,n);
                for(int i=0;i<n;i++){
                    var unit=nodes[i].unit;if(unit==null||x[i]<=0||links[i].Count==0||clocks[i]>0.0000001||unit.combatDefinition.squadShotDamage<=0)continue;
                    Func<int,bool> eligible=j=>{if(x[j]<=0)return false;if(nodes[j].unit!=null){var cover=c.CoveringFort(nodes[j].unit);if(cover!=null)foreach(var other in links[i])if(nodes[other.target].fort==cover&&x[other.target]>0)return false;}return true;};
                    double total=0,weight=1;foreach(var link in links[i])if(eligible(link.target)){total+=weight;weight*=r.distanceDecay;}if(total<=0)continue;
                    float buff=1;var fort=c.ProtectingFort(unit);if(fort!=null)for(int p=0;p<n;p++)if(nodes[p].fort==fort&&x[p]>0){buff=r.fortAttackMultiplier*Mathf.Pow(r.fortLevelAttackFactor,fort.level);break;}
                    double output=unit.combatDefinition.squadShotDamage*x[i]/Math.Max(.000001,unit.combatDefinition.squadHealth)*buff;weight=1;
                    foreach(var link in links[i]){int j=link.target;if(!eligible(j))continue;double amount=output*weight/total;weight*=r.distanceDecay;
                        if(nodes[j].unit!=null){amount*=incoming[j]*flanks[i,j]*encircle[j]*Mathf.Clamp01(nodes[j].unit.combatDefinition.incomingDamageFactor);if(amount>0)nodes[j].unit.GetComponent<CampaignUnit>().ObserveAttack(unit);}
                        if(unit.combatDefinition.role==UnitRole.Engineer)amount*=nodes[j].fort!=null?r.engineerFortDamageMultiplier:r.engineerOrdinaryAttackMultiplier;
                        if(amount>0){shotLoss[j]+=amount;firedLinks[i,j]=true;}
                    }
                    clocks[i]=Math.Max(.01,unit.combatDefinition.attackIntervalSeconds);
                }
                for(int i=0;i<n;i++)x[i]=Math.Max(0,x[i]-shotLoss[i]);
            }
            for(int i=0;i<n;i++)if(nodes[i].unit!=null)nodes[i].unit.GetComponent<CampaignUnit>().AttackRemaining=(float)clocks[i];

            // Notify visible firing before applying terminal damage; no effect can control the solver.
            for(int i=0;i<n;i++)if(links[i].Count>0&&nodes[i].unit!=null&&initial[i]>0)
            {
                var attacker=nodes[i].unit;bool fired=false;foreach(var link in links[i])if(firedLinks[i,link.target]){var t=nodes[link.target];if(t.unit!=null){w.Intel.Identify(attacker.ownerId,t.unit);w.Intel.Identify(t.owner,attacker);}if(w.revealForestOnAttack)attacker.RevealForest();w.NotifyCombatEffect(CombatEffectEvent.Direct(attacker,t.id,t.owner,t.fort==null?t.position:t.fort.ClosestPoint(nodes[i].position),t.collector!=null));fired=true;}
                if(fired)attacker.Engage();
            }
            foreach(var u in w.units)if(u!=null&&u.State==ArmyBattleState.Engaged)u.ReleaseCombat();
            for(int i=0;i<n;i++){float loss=(float)(initial[i]-x[i]);if(loss<=0)continue;if(nodes[i].unit!=null)nodes[i].unit.Damage(loss);else if(nodes[i].fort!=null)nodes[i].fort.Damage(loss);else nodes[i].collector.Damage(loss);}
        }
    }
}






