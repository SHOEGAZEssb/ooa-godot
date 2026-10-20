using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCheepCheepPublicationRom()
    {
        foreach(int subid in new[]{0,1})
        foreach(int distance in new[]{0,1,0x20,0x60,0x80,0x81,0xff})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(7,0x0a);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,104));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            Vector2 initial=new(4+(fractional?.25f:0),250+(fractional?.5f:0));
            var actor=new CheepCheepCharacter();actor.Initialize(EnemyDatabase.Shared.ImportedEnemy(0x2c,subid),_currentRoom,initial.Floor(),distance,_random);
            var source=new EnemyCombatSourceDescriptor(0x2c,subid,0x94,0,1,EnemyHandlerKind.CheepCheep,"cheepcheep.s:enemyCode2c");
            var adapter=new CheepCheepRoomEntity(actor,source,_sound.PlaySound);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0,reversals=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xa4;
            const int at=0xd180;native[at]=1;native[at+1]=0x2c;native[at+2]=(byte)subid;native[at+3]=(byte)distance;
            native.Word(at+10,(int)(initial.Floor().Y*256));native.Word(at+12,(int)(initial.Floor().X*256));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int old=native[at+9];native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(expected[3]!=0xa8)moves++;if(updates>2&&native[at+9]!=old)reversals++;
                FailIf(actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    SomariaPrivate<int>(actor,"_travelCounter")!=native[at+3]||
                    SomariaPrivate<Vector2>(actor,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||actor.AnimationIndex!=native[at+8]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Cheep$2c:${subid:x2}/var03${distance:x2}/fraction{fractional}/update{updates}: original patrol, bytes/counters, reversal or phase scratch differs.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Cheep Cheep must consume the original common state0 draw, with no patrol RNG.");
                probes=0;
            });
            Step(1);
            // Fresh source allocation has zero low bytes. Declare retained
            // fractional words only after common initialization completes.
            if(fractional)
            {
                F1Set(actor,"_precisePosition",initial);
                native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            }
            Step(1);Step(1);
            _dialogue.ShowMessage("Cheep patrol hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            int doubled=(distance*2)&255,travel=doubled==0?256:doubled;
            Step(2*(travel+60)-1);Step(2);
            FailIf(moves!=2*travel+2||reversals!=2||SomariaPrivate<Vector2>(actor,"_precisePosition")!=
                initial+(subid==0?Vector2.Left:Vector2.Down),
                "Cheep must complete two exact movement/rest cycles and resume the third without RNG or drift.");
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
