using UnityEngine;
namespace CampaignDemo {
 // Original procedural starter sounds. No third-party recordings or paid assets.
 // Replace profile clips with licensed recordings without changing spatial logic.
 public static class BattlefieldAudioSynthesis {
  public static int VariantCount(BattlefieldSound kind){return kind==BattlefieldSound.Bird||kind==BattlefieldSound.Footstep?8:kind==BattlefieldSound.Telegraph?6:kind==BattlefieldSound.Construction?4:1;}
  static float Envelope(float t,float length,float attack){return Mathf.Clamp01(t/attack)*Mathf.Clamp01((length-t)/attack);}
  public static AudioClip Create(BattlefieldSound kind,int variation=0){
   variation=Mathf.Abs(variation)%VariantCount(kind);
   const int rate=22050;bool engine=kind==BattlefieldSound.Car||kind==BattlefieldSound.Tank;
   float duration=engine?2:kind==BattlefieldSound.Telegraph?4:kind==BattlefieldSound.Bird?1.1f+variation*.07f:kind==BattlefieldSound.Footstep?.78f:kind==BattlefieldSound.Construction?.48f:kind==BattlefieldSound.Rifle?.45f:1.6f;
   string[] messages={".- .. -","-- . ...",".. .- ..","- .. -- .",". ... - .",".. -- .. -"};
   var data=new float[Mathf.CeilToInt(rate*duration)];var rng=new System.Random(471+(int)kind+variation*137);float low=0,body=0,birdPhase=0;
   var feet=new float[6];var grit=new float[6];var offsets=new float[6];
   for(int person=0;person<6;person++)offsets[person]=person*.095f+(float)rng.NextDouble()*.025f;
   for(int i=0;i<data.Length;i++){float t=i/(float)rate,noise=(float)rng.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.07f);float s=0;
    // No continuous noise layer in vehicles; keep engine and mechanical tread.
    if(engine){float frequency=kind==BattlefieldSound.Tank?45:65;float phase=2*Mathf.PI*frequency*t;s=.2f*Mathf.Sin(phase)+.08f*Mathf.Sin(phase*2)+.035f*Mathf.Sin(phase*3);if(kind==BattlefieldSound.Car)s+=low*.6f;if(kind==BattlefieldSound.Tank)s+=.08f*Mathf.Sin(2*Mathf.PI*210*t)*Mathf.Exp(-(t%(1f/11))*150);}
    else if(kind==BattlefieldSound.Telegraph){float dot=.065f+variation*.004f,cursor=0;foreach(char symbol in messages[variation]){if(symbol==' '){cursor+=dot*3;continue;}float length=dot*(symbol=='-'?3:1),local=t-cursor;if(local>=0&&local<length){s=.3f*Mathf.Sin(2*Mathf.PI*(610+variation*31)*t)*Envelope(local,length,.006f);break;}cursor+=length+dot;}}
    else if(kind==BattlefieldSound.Bird){
     float period=.22f+(variation%3)*.055f,local=t%period,length=.065f+(variation%4)*.024f,u=Mathf.Clamp01(local/length),frequency=1800+variation*210;
     switch(variation%4){case 0:frequency+=1300*u;break;case 1:frequency+=1600*(1-u);break;case 2:frequency+=1000*Mathf.Sin(Mathf.PI*u);break;default:frequency+=500*Mathf.Sin(4*Mathf.PI*u);break;}
     birdPhase+=2*Mathf.PI*frequency/rate;
     if(local<length&&Mathf.FloorToInt(t/period)<2+variation%4)s=(Mathf.Sin(birdPhase)+.12f*Mathf.Sin(birdPhase*2))*Mathf.Pow(Mathf.Sin(Mathf.PI*u),1.4f)*.28f;
    }
    else if(kind==BattlefieldSound.Footstep){
     // Six independently textured, staggered walkers inside one pooled voice.
     for(int person=0;person<6;person++){float local=t-offsets[person];if(local<0||local>.24f)continue;
      float n=(float)rng.NextDouble()*2-1;feet[person]=Mathf.Lerp(feet[person],n,.018f+(variation+person)*.001f);grit[person]=Mathf.Lerp(grit[person],n,.07f);
      float heel=Mathf.Exp(-local*(40+person*2))*Mathf.Clamp01(local/.006f),toeTime=local-(.045f+person*.002f);
      float toe=toeTime>0?Mathf.Exp(-toeTime*55)*Mathf.Clamp01(toeTime/.008f):0;
      s+=(feet[person]*(heel*3.8f+toe*2.2f)+grit[person]*.12f*Mathf.Exp(-local*65)*Mathf.Clamp01(local/.004f))*(person==0?1:.65f);
     }
    }
    else if(kind==BattlefieldSound.Construction){float hz=1300+variation*170;s=(Mathf.Sin(2*Mathf.PI*hz*t)*.32f+Mathf.Sin(2*Mathf.PI*hz*1.49f*t)*.15f+Mathf.Sin(2*Mathf.PI*hz*2.13f*t)*.08f)*Mathf.Exp(-t*13)+noise*.1f*Mathf.Exp(-t*120);}
    else {float decay=kind==BattlefieldSound.Rifle?18:4;float hz=kind==BattlefieldSound.Rifle?130:48;s=(noise*.24f*Mathf.Exp(-t*decay*2)+low*.9f*Mathf.Exp(-t*decay)+Mathf.Sin(2*Mathf.PI*hz*t)*.4f*Mathf.Exp(-t*decay*1.5f));}
    if(!engine)s*=Mathf.Clamp01(t/.003f)*Mathf.Clamp01((duration-t)/.025f);data[i]=Mathf.Clamp(s,-.95f,.95f);
   }
   // Short impulses were far quieter than the sustained radio tone. Normalize
   // their peak before applying the category gain and map-distance attenuation.
   float peak=0;foreach(float sample in data)peak=Mathf.Max(peak,Mathf.Abs(sample));
   if(peak>.0001f){float scale=.85f/peak;for(int i=0;i<data.Length;i++)data[i]*=scale;}
   var clip=AudioClip.Create("Procedural "+kind,data.Length,1,rate,false);clip.SetData(data,0);return clip;
  }
 }
}
