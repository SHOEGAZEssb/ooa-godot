using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFloormasterPlacementPublicationRom(int firstAngle, int angleCount)
    {
        int cases = 0;
        foreach(int room in new[]{0x19,0x3a,0xa7})
        {
            var supported=_rooms.GetRoom(5,room);
            FailIf(supported.Width!=240||supported.Height!=176,
                $"Source Floormaster room$5:${room:x2} must retain its large-room collision-buffer boundary.");
        }
        foreach(int angle in Enumerable.Range(firstAngle,angleCount))
        foreach(Vector2 link in new[]{new Vector2(88,88),new Vector2(232,88),new Vector2(88,8)})
        foreach(int sub in new[]{1,2})
        foreach(bool blocked in new[]{false,true})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            if(link!=new Vector2(88,88)&&(angle&7)!=0)continue;
            InitializeCompletedIntroRoom(5, 0xa7);
            FillValidationRoom(0, (byte)(blocked ? 15 : 0));
            _currentRoom.SetPositionTileAndCollision(link,0,0,0);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var data=EnemyDatabase.Shared;var parent=new FloormasterCharacter();
            try
            {
            var actualWrites=new List<uint>();var expectedWrites=new List<uint>();bool capture=false;
            uint last=0;
            uint RuntimeScratch() => (uint)_runtimeState.ReadWramByte(0xcec0) |
                (uint)_runtimeState.ReadWramByte(0xcec1) << 8 | (uint)_runtimeState.ReadWramByte(0xcec2) << 16 |
                (uint)_runtimeState.ReadWramByte(0xcec3) << 24;
            void Observe()
            {
                if(!capture)return;uint value=RuntimeScratch();if(value==last)return;
                actualWrites.Add(value);last=value;
            }
            _runtimeState.Changed+=Observe;
            try
            {
            parent.Initialize(data.ImportedEnemy(0x35,0),_currentRoom,new(16,48),_random);
            F1Set(parent,"<State>k__BackingField",1);F1Set(parent,"<LiveChildren>k__BackingField",3);
            F1Set(parent,"<RemainingChildren>k__BackingField",3);F1Set(parent,"<LastLinkPosition>k__BackingField",link);
            var actor=new FloormasterCharacter();actor.Initialize(data.ImportedEnemy(0x35,sub),_currentRoom,
                fractional?new(.25f,.5f):Vector2.Zero,_random,()=>parent);
            var source=data.EnemyHandlers.ResolveHandler(0x35,sub,"floormaster.s:placement boundary")
                .CombatSource(2,0,"floormaster.s:state8");
            var adapter=new FloormasterRoomEntity(actor,source,_sound.PlaySound,()=>_random.Next().Value,()=>_entities.PartSlotAvailable);
            var before=new PartMovementScratchProbe(()=>
            {
                capture=false;for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                actualWrites.Clear();last=RuntimeScratch();capture=true;
            });
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,2);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;capture=false;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xa2;
            const int owner=0xd180,at=0xd280;native[owner]=1;native[owner+1]=0x35;native[owner+4]=1;
            native.ScratchChanged=()=>expectedWrites.Add(Enumerable.Range(0,4).Aggregate(0u,(packed,i)=>packed|((uint)native[0xcec0+i]<<(i*8))));
            native[owner+0x29]=17;native[owner+0x30]=native[owner+0x33]=3;
            native[owner+0x31]=(byte)link.Y;native[owner+0x32]=(byte)link.X;
            native[at]=1;native[at+1]=0x35;native[at+2]=(byte)sub;native[at+0x16]=0x80;native[at+0x17]=0xd1;
            if(fractional){native[at+10]=0x80;native[at+12]=0x40;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                expectedWrites.Clear();
                native[0xd009]=(byte)_player.LinkMovementAngle;
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||parent.LastLinkPosition!=new Vector2(native[owner+0x32],native[owner+0x31])||
                    probes!=1||!observed.SequenceEqual(expected)||!actualWrites.SequenceEqual(expectedWrites),
                    $"Floormaster child${sub:x2}/angle${angle:x2}/link{link}/blocked{blocked}/fraction{fractional}/update{updates}: original placement differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{actor.State}/{native[at+4]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "Floormaster placement must consume exactly the original common and near-Link RNG draws.");probes=0;
            });
            Step(1);var current=_random.CaptureState();bool found=false;
            for(int candidate=0;candidate<256;candidate++)
            {
                var trial=current with {Rng1=(byte)candidate,Rng2=1};var prediction=new OracleRandom();prediction.RestoreState(trial);
                if(((prediction.Next().Value+_player.LinkMovementAngle)&31)!=angle)continue;
                _random.RestoreState(trial);native[0xff94]=trial.Rng1;native[0xff95]=trial.Rng2;found=true;break;
            }
            FailIf(!found,"Floormaster placement fixture requires an original RNG seed for each projected angle.");
            int trialsBefore=native.CircleCalls;Step(1);
            if(blocked&&link==new Vector2(88,88)&&angle==0)
                FailIf(actor.State!=8||native.CircleCalls-trialsBefore!=4||native[0xcec1]!=0xf0,
                    "A fully rejected upward Floormaster attempt must publish all four native trials and retain radius$10 scratch.");
            _dialogue.ShowMessage("Floormaster placement hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            // Re-enter the same declared placement boundary; the previous attempt
            // has copied Link's position to the authoritative parent's cache.
            F1Set(actor,"<State>k__BackingField",8);native[at+4]=8;Step(2);
            _entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
            }
            finally{_runtimeState.Changed-=Observe;}
            }
            finally{parent.Free();}
            cases++;
        }
        int cardinalAngles = Enumerable.Range(firstAngle, angleCount).Count(angle => (angle & 7) == 0);
        VerifyValidationMatrixCases(nameof(ValidateFloormasterPlacementPublicationRom), firstAngle, angleCount, 32, cases,
            angleCount * 16 + cardinalAngles * 32);
    }
}
