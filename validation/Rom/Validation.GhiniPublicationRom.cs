using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGhiniPublicationRom()
    {
        foreach(int angle in new[]{0,8,16,24})
        foreach(bool atBoundary in new[]{false,true})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0x1e);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,88));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            Vector2 start=!atBoundary?new(80,80):angle switch
            {0=>new(80,3),8=>new(_currentRoom.Width-6,64),16=>new(80,_currentRoom.Height-6),_=>new(5,64)};
            if(fractional)start+=new Vector2(.25f,.75f);
            var data=EnemyDatabase.Shared;var actor=new GhiniCharacter();actor.Initialize(data.ImportedEnemy(0x17,0),_currentRoom,start,_random);
            var source=data.EnemyHandlers.ResolveHandler(0x17,0,"ghini.s:subid00")
                .CombatSource(0,0,"ghini.s:subid00");
            var adapter=new GhiniRoomEntity(actor,source,_sound.PlaySound);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,bounces=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x17*4];
            const int at=0xd180;native[at]=1;native[at+1]=0x17;
            native.Word(at+10,(int)(start.Y*256));native.Word(at+12,(int)(start.X*256));
            int SourceState()=>actor.State switch{GhiniState.Uninitialized=>0,GhiniState.Choosing=>8,_=>9};
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldAngle=native[at+9],oldState=native[at+4];
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(oldState==9&&oldAngle!=native[at+9])bounces++;
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SourceState()!=native[at+4]||SomariaPrivate<int>(actor,"_counter")!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||actor.AnimationParameter!=native[at+0x21]||
                    SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||probes!=1||!observed.SequenceEqual(expected),
                    $"Ghini$17:00/angle${angle:x2}/boundary{atBoundary}/fraction{fractional}/update{updates}: original motion differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{actor.Angle}/{native[at+9]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    $"Ghini/update{updates}: source common/route RNG differs: calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");probes=0;
            });
            _dialogue.ShowMessage("Ghini state0 hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            var current=_random.CaptureState();bool found=false;
            for(int candidate=0;candidate<256;candidate++)
            {
                var trial=current with {Rng1=(byte)candidate,Rng2=(byte)candidate};var prediction=new OracleRandom();prediction.RestoreState(trial);
                if((prediction.Next().High&24)!=angle)continue;
                _random.RestoreState(trial);native[0xff94]=trial.Rng1;native[0xff95]=trial.Rng2;found=true;break;
            }
            FailIf(!found,"Ghini fixture must select each route using original shared RNG.");
            Step(2);_runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            int remaining=200;while(actor.State==GhiniState.Moving&&remaining-->0)Step(1);
            FailIf(actor.State!=GhiniState.Choosing||atBoundary&&bounces==0,"Ghini must finish its native route and retain source boundary bounces without clamping.");
            Step(1);FailIf(actor.State!=GhiniState.Moving,"Ghini must select another route after completion.");
            Step(8);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
