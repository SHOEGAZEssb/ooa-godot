using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrowPublicationRom()
    {
        var raw=new ObjectMotionRom();
        for(int angle=0;angle<256;angle++)
        {
            raw[ObjectMotionRom.Object+9]=(byte)angle;raw[ObjectMotionRom.Object+16]=0x32;
            raw.Word(ObjectMotionRom.Object+10,0);raw.Word(ObjectMotionRom.Object+12,0);raw.Call(ObjectMotionRom.ApplySpeed);
            var velocity=NativeObjectMovement.Velocity(_runtimeState,0x32,angle);
            FailIf(velocity.YFixed!=unchecked((short)raw.Word(ObjectMotionRom.Object+10))||
                velocity.XFixed!=unchecked((short)raw.Word(ObjectMotionRom.Object+12))||
                Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=raw[0xcec0+i]),
                $"Crow raw angle${angle:x2}: original table alias or invalid-angle scratch differs.");
        }
        bool sawUnderflow=false,sawOverflow=false;
        foreach(int randomByte in Enumerable.Range(0,8))
        foreach(int direction in Enumerable.Range(0,5))
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(0,0x5d);
            var record=_entities.Entities<CrowCharacter>().Single().Record;_entities.Clear();
            var data=new EnemyDatabase();var row=data.GetRoomObjects(0,0x5d).Single(r=>r.Id==0x41);
            var source=data.EnemyHandlers.ResolveHandler(row).CombatSource(row,0);
            var actor=new CrowCharacter();actor.Initialize(record,_currentRoom,new(80,64),_random);
            var adapter=new CrowRoomEntity(actor,source,_sound.PlaySound);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(144,112));_player.BeginCutsceneControl(owner:actor);
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState() with {Rng1=(byte)randomByte};_random.RestoreState(seed);
            var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0x93;
            const int at=0xd180;native[at]=1;native[at+1]=0x41;native[at+11]=64;native[at+13]=80;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int calls=native.MovementCalls;native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;if(native.MovementCalls!=calls)moves++;
                FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(
                    (int)actor.State+8!=native[at+4]||actor.Counter1!=native[at+6]||actor.Counter2!=native[at+7]||actor.Angle!=native[at+9]||
                    actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||actor.Z!=unchecked((sbyte)native[at+15])||
                    actor.AnimationIndex!=native[at+0x31]||actor.Visible!=((native[at+0x1a]&128)!=0)||
                    actor.CollisionEnabled!=((native[at+0x24]&128)!=0))||probes!=1||!observed.SequenceEqual(expected),
                    $"Crow$41 seed${randomByte:x2}/target{direction}/update{updates}: original init/rise/charge/raw angle/animation/bounds or scratch differs: state{actor.State}/${native[at+4]:x2},angle${actor.Angle:x2}/${native[at+9]:x2},animation{actor.AnimationIndex}/{native[at+0x31]}.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Crow must consume exactly the original common initialization and initial aim-offset RNG draws.");probes=0;
            });
            Step(3);
            Vector2 target=direction switch {0=>new(80,16),1=>new(104,64),2=>new(80,112),3=>new(56,64),_=>new(72,16)};
            if(direction==4)
            {
                var angle=new ObjectMotionRom();angle.Word(ObjectMotionRom.Object+10,64*256);angle.Word(ObjectMotionRom.Object+12,80*256);
                bool found=false;
                for(int x=56;x<80&&!found;x++)
                {
                    angle.Call(ObjectMotionRom.RelativeAngle,targetY:16,targetX:x);
                    if(angle.Result!=31)continue;target=new(x,16);found=true;
                }
                FailIf(!found,"Crow positive angle overflow fixture requires original relative angle$1f inside the approach box.");
            }
            _player.WarpTo(target);Step(1);
            FailIf(actor.State!=CrowState.Rising||actor.Counter1!=25,"Crow source approach must select its25-update rise.");
            Step(24);_dialogue.ShowMessage("Crow rise hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(1);sawUnderflow|=native[at+9]>=0x80;sawOverflow|=native[at+9] is >=32 and <128;
            while(!actor.IsDead&&updates<300)Step(1);
            Step(2);FailIf(!actor.IsDead||!actor.DeletedOutOfBounds||moves<5,"Crow must complete its source charging/bounds retirement and stay retired.");
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl(actor);
        }
        FailIf(!sawUnderflow||!sawOverflow,"Crow must exercise original initial negative and positive raw-angle overflow.");
    }
}
