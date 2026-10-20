using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrowChargeCountersRom()
    {
        foreach(int angle in new[]{0,8,16,24})
        foreach(int counter in new[]{0,1,8,9,17})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(0,0x5d);
            var record=_entities.Entities<CrowCharacter>().Single().Record;_entities.Clear();
            var data=EnemyDatabase.Shared;var row=data.GetRoomObjects(0,0x5d).Single(r=>r.Id==0x41);
            var actor=new CrowCharacter();actor.Initialize(record,_currentRoom,new(80,64),_random);
            var adapter=new CrowRoomEntity(actor,data.EnemyHandlers.ResolveHandler(row).CombatSource(row,0),_sound.PlaySound);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(80,64));_player.BeginCutsceneControl(owner:actor);
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,zeroVisits=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0x93;
            const int at=0xd180;native[at]=1;native[at+1]=0x41;native[at+11]=64;native[at+13]=80;
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>native.Update(_entities.FrameCounter,_player.Position));probes=0;
            F1Set(actor,"_state",CrowState.Charging);F1Set(actor,"_counter2",counter);F1Set(actor,"_angle",angle);
            F1Set(actor,"_collisionEnabled",true);F1Set(actor,"_z",-6);F1Set(actor,"_precisePosition",new Vector2(80.25f,64.5f));
            actor.Position=new(80,64);native[at+4]=10;native[at+7]=(byte)counter;native[at+9]=(byte)angle;
            native[at+0x24]|=0x80;native[at+15]=0xfa;native[at+0x30]=2;native[at+10]=0x80;native[at+12]=0x40;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(native[at]!=0&&native[at+7]==0)zeroVisits++;
                FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(
                    actor.Counter2!=native[at+7]||actor.Angle!=native[at+9]||
                    actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.AnimationIndex!=native[at+0x31])||probes!=1||!observed.SequenceEqual(expected),
                    $"Crow charge angle${angle:x2}/counter${counter:x2}/update{updates}: original zero saturation, steering cadence, wrapping or scratch differs.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Declared Crow charge must not consume extra RNG after original initialization.");probes=0;
            });
            Step(2);_dialogue.ShowMessage("Crow charge hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            while(!actor.IsDead&&updates<220)Step(1);
            Step(2);FailIf(!actor.IsDead||zeroVisits<3,"Crow must reach and retain counter2 zero across multiple movements before silent boundary deletion.");
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl(actor);
        }
    }
}
