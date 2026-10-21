using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinBombPublicationRom()
    {
        int settled=0,explodedInFlight=0;
        foreach(int form in new[]{0,1,2})
        foreach(int angle in form==0?Enumerable.Range(12,8):form==1?new[]{0,8,16,24}:new[]{0x0a,0x16})
        foreach(bool shortFuse in form==0?new[]{false,true}:new[]{false})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xaf);
            var template=_entities.Entities<KingMoblinBoss>().Single();var parent=new KingMoblinBoss();
            parent.Initialize(template.World,new(80,32));_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(16,120));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            KingMoblinMinion? minion=null;
            if(form!=0)
            {
                minion=new KingMoblinMinion();minion.Initialize(parent,0);minion.Position=new(24,8);
                // The preceding held-bomb visit can observe this fractional
                // minion height on its jump, before the apex releases the bomb.
                F1Set(minion,"_z",form==1?-0x9a0:0);
            }
            var actor=new KingMoblinBomb();actor.Initialize(parent,minion);
            if(fractional)
            {
                actor.Position+=new Vector2(.25f,.5f);
                var motion=new CarriedObjectMotion(actor.Position){ZFixed=0x80};F1Set(actor,"_motion",motion);
            }
            var adapter=new KingMoblinBombRoomEntity(actor);F1Parts.Add(adapter,0);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,16,120);
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xd024]=0;native[0xcc08]=(byte)(form==0?0xab:0x78);
            // Declared healthy stationary parent fields isolate the PART's
            // complete motion handler. Minion/King AI dispatch is separate.
            native[0xd0a9]=6;native[0xd18b]=8;native[0xd18d]=24;native.Word(0xd18e,form==1?-0x9a0:0);
            const int at=0xd0c0;native[at]=1;native[at+1]=(byte)(form==0?0x3f:0x47);
            native.Word(at+10,(int)(actor.Position.Y*256));native.Word(at+12,(int)(actor.Position.X*256));native.Word(at+14,fractional?0x80:0);
            native.Word(at+0x16,form==0?0xd080:0xd180);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,moves=0;var sounds=_sound.AttachPlayRequestAudit();
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,value);native[0xcec0+i]=value;}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int oldState=native[at+4];bool alive=native[at]!=0;int oldX=native.Word(at+12),oldY=native.Word(at+10);
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(alive&&native[at]!=0&&(oldState==(form==0?3:2))&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(
                    actor.State!=native[at+4]||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (actor.ZFixed&65535)!=native.Word(at+14)||
                    (SomariaPrivate<CarriedObjectMotion>(actor,"_motion").SpeedZ&65535)!=native.Word(at+0x14)||
                    actor.AnimationParameter!=native[at+0x21]||SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||
                    form==0&&(actor.Fuse!=native[at+6]||actor.Flashes!=native[at+7]))||
                    observedCapture.Samples!=1||Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"King bomb${(form==0?0x3f:0x47):x2}/form{form}/angle${angle:x2}/fast{shortFuse}/fraction{fractional}/update{updates}: state{actor.State}/{native[at+4]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{actor.ZFixed}/{unchecked((short)native.Word(at+14))}, fuse{actor.Fuse}/{native[at+6]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||!sounds.Requests.SequenceEqual(native.Sounds),
                    $"King bomb/form{form}/update{updates}: original initialization RNG or fuse/bounce/explosion cues differ [{string.Join(',',sounds.Requests)}]/[{string.Join(',',native.Sounds)}].");
                observedCapture.Samples=0;Seed();
            });
            try
            {
                Seed();Step(1);
                actor.Throw(angle,form==0?-0x180:form==2?-0x100:null,form==2?0x37:null,terrainEffects:form!=2);
                native[at+4]=(byte)(form==0?3:2);native[at+9]=(byte)angle;
                if(form!=1)native.Word(at+0x14,form==0?-0x180:-0x100);
                if(form==2){native[at+16]=0x37;native[at+0x1a]=0x81;}
                if(shortFuse)
                {
                    F1Set(actor,"<Fuse>k__BackingField",0);F1Set(actor,"<Flashes>k__BackingField",11);
                    native[at+6]=0;native[at+7]=11;
                }
                Step(1);
                _dialogue.ShowMessage("King bomb hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
                while(native[at]!=0&&updates<800)
                {
                    int oldState=native[at+4];Step(1);
                    if(form==0&&oldState==3&&native[at+4]==4)settled++;
                    if(form==0&&oldState==3&&native[at+4]==5)explodedInFlight++;
                }
                FailIf(!actor.IsDead||!shortFuse&&moves<8,"Both bomb handlers must complete their source motion, fuse/explosion and deletion paths.");
                Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
            }
            finally{minion?.Free();parent.Free();}
        }
        FailIf(settled==0||explodedInFlight==0,"Large bombs must cover final no-motion landing and fuse expiry before motion.");
    }
}
