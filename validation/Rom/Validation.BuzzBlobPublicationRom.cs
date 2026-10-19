using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBuzzBlobPublicationRom()
    {
        foreach(int randomByte in Enumerable.Range(0,8))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(0,0x43);
            var adapter=_entities.EntityAdapters<BuzzBlobRoomEntity>().Single();var actor=(BuzzBlobCharacter)adapter.Node;F1Retain(adapter);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(144,112));_player.BeginCutsceneControl(owner:actor);
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var seed=_random.CaptureState() with {Rng1=(byte)randomByte};_random.RestoreState(seed);
            var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            native[0xcc08]=0x93;
            int slot=SomariaPrivate<System.Collections.Generic.Dictionary<IRoomEntity,int>>(_entities,"_enemySlots")[adapter];
            int at=0xd080+slot*256;native[at]=1;native[at+1]=0x18;
            native.Word(at+10,(int)(actor.Position.Y*256));native.Word(at+12,(int)(actor.Position.X*256));
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0,choices=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int old=native[at+4];native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(expected[3]!=0xa8)moves++;if(old==8&&native[at+4]==9)choices++;
                FailIf(actor.IsDead||native[at]==0||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Buzz Blob$18 seed${randomByte:x2}/fraction{fractional}/update{updates}: original roaming, counter, bounce/admission or scratch differs.");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Buzz Blob must preserve original common initialization and high/low movement-choice RNG draws.");
                probes=0;
            });
            Step(1);
            if(fractional){actor.Position+=new Vector2(.25f,.5f);native[at+10]=0x80;native[at+12]=0x40;}
            Step(6);_dialogue.ShowMessage("Buzz Blob text hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(240);
            FailIf(moves<180||choices<3,"Buzz Blob must exercise repeated source movement choices and exact counter-zero no-motion visits.");
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl(actor);
        }
    }
}
