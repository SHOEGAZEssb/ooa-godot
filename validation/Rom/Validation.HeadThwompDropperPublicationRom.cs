using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeadThwompDropperPublicationRom()
    {
        var seeds=new Dictionary<int,(byte Rng1,byte Rng2)>();
        var trial=new OracleRandom();var state=trial.CaptureState();
        for(int value=0;value<65536&&seeds.Count<32;value++)
        {
            trial.RestoreState(state with {Rng1=(byte)value,Rng2=(byte)(value>>8)});
            var roll=trial.Next();int key=(roll.Value&3)|((roll.Value&0x60)>>3)|((roll.High&0x10)<<1);
            seeds.TryAdd(key,((byte)value,(byte)(value>>8)));
        }
        FailIf(seeds.Count!=32,"The native dropper RNG must cover four speeds, four vertical words and both directions.");
        int landings=0,pickupLosses=0;
        foreach(var test in seeds.OrderBy(pair=>pair.Key))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x2b);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(16,160));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var seed=_random.CaptureState() with {Rng1=test.Value.Rng1,Rng2=test.Value.Rng2};_random.RestoreState(seed);
            var pickup=_entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.Bombs,new(120,72)));
            var helper=new HeadThwompBombDropRoomEntity(_currentRoom,_random,()=>pickup,new(120,72),_runtimeState);
            F1Parts.Add(helper,1);_entities.AddEntity(helper);
            if(fractional)F1Set(helper,"_position",new OracleObjectPosition(0x4880,0x7840));
            var native=new SomariaRom(_saveData,seed,_currentRoom,0,16,160);native.InitializeLinkGameplay();native[0xcd00]=0;
            native[0xd0c0]=1;native[0xd0c1]=1;native[0xd0c2]=4;native[0xd0cb]=72;native[0xd0cd]=120;
            const int at=0xd1c0;native[at]=1;native[at+1]=0x40;native.Word(at+10,fractional?0x4880:0x4800);native.Word(at+12,fractional?0x7840:0x7800);
            native.Word(at+0x16,0xd0c0);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,moves=0;
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,value);native[0xcec0+i]=value;}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int oldX=native.Word(at+12);bool wasAlive=native[at]!=0;
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(wasAlive&&native[at]!=0&&oldX!=native.Word(at+12))moves++;
                var position=SomariaPrivate<OracleObjectPosition>(helper,"_position");
                FailIf(helper.Finished!=(native[at]==0)||!helper.Finished&&(
                    position.YFixed!=native.Word(at+10)||position.XFixed!=native.Word(at+12)||
                    SomariaPrivate<int>(helper,"_speed")!=native[at+16]||SomariaPrivate<int>(helper,"_angle")!=native[at+9]||
                    (SomariaPrivate<int>(helper,"_speedY")&65535)!=native.Word(at+0x14))||
                    pickup.Finished!=(native[0xd0c0]==0)||!pickup.Finished&&pickup.PrecisePosition!=new Vector2(native.Word(0xd0cc)/256f,native.Word(0xd0ca)/256f)||
                    observedCapture.Samples!=1||Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"Head Thwomp$40/route{test.Key}/fraction{fractional}/update{updates}: helper words{position}/{native.Word(at+10):x4},{native.Word(at+12):x4}, pickup{pickup.PrecisePosition}/{native.Word(0xd0cc)/256f},{native.Word(0xd0ca)/256f}, finished{helper.Finished}/{native[at]==0}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "Bomb carrier must consume its original single initialization draw and no repeated draw on holds or landing.");
                observedCapture.Samples=0;Seed();
            });
            int hold=test.Key&3;
            if(hold==0){_dialogue.ShowMessage("Head helper state0.",104);native[0xcba0]=1;}
            if(hold==1){_runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;}
            if(hold==2){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Seed();Step(1);
            if(hold==0){_dialogue.Close();native[0xcba0]=0;}
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(1);
            _dialogue.ShowMessage("Head Thwomp helper hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at]!=0&&updates<140)Step(1);
            FailIf(!helper.Finished||moves<10,"Original landing or related-pickup deletion must release the carrier after a real flight, without terminal planar/copy work.");
            if(pickup.Finished)pickupLosses++;else landings++;
            Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
        FailIf(landings==0||pickupLosses==0,"The room's original launch choices must exercise surviving pickups and related-pickup deletion separately.");
    }
}
