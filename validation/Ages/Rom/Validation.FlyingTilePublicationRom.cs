using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFlyingTilePublicationRom()
    {
        Vector2I[] directions=[new(0,-1),new(1,-1),new(1,0),new(1,1),new(0,1),new(-1,1),new(-1,0),new(-1,-1)];
        foreach(var direction in directions)
        foreach(Vector2 start in new[]{new Vector2(120,88),new Vector2(232,168)})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(4, 0x5b);
            Vector2 target=start+(Vector2)direction*32;
            target=new(Math.Clamp(target.X,16,224),Math.Clamp(target.Y,16,160));
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(target);_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var data=EnemyDatabase.Shared;var actor=_entities.Spawn<FlyingTileCharacter>(
                new FlyingTileSpawn(data.ImportedEnemy(0x52,0),start,true));
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,moves=0;
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x52*4];
            const int at=0xd180;native[at]=1;native[at+1]=0x52;native[at+2]=0x80;
            native[at+11]=(byte)start.Y;native[at+13]=(byte)start.X;
            int packed=((int)start.Y&0xf0)|((int)start.X>>4);
            int NativeState()=>(int)actor.State;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldMoves=native.MovementCalls;
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;moves+=native.MovementCalls-oldMoves;
                var precise=SomariaPrivate<Vector2>(actor,"_precisePosition");
                FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(
                    NativeState()!=native[at+4]||precise!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||actor.ZFixed!=unchecked((short)native.Word(at+14))||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||actor.AnimationParameter!=native[at+0x21])||
                    _currentRoom.GetMetatile(start)!=native[0xcf00+packed]||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Flying Tile/start{start}/direction{direction}/fraction{fractional}/update{updates}: original init/charge differs: state{NativeState()}/{native[at+4]}, XY{precise}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Flying Tile/update{updates}: original common/debris RNG differs: calls{_random.CaptureState().Calls-seed.Calls}/{native.RandomCalls}.");observedCapture.Samples=0;
            });
            _dialogue.ShowMessage("Flying Tile common init hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            Step(1);
            if(fractional){F1Set(actor,"_precisePosition",start+new Vector2(.25f,.5f));native[at+10]=0x80;native[at+12]=0x40;}
            Step(7);Step(14);FailIf(actor.State!=FlyingTileState.Waiting||actor.Counter!=1,
                "Flying Tile must retain its fifteenth aim-wait update before charging.");
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            Step(1);FailIf(actor.State!=FlyingTileState.Charging||moves!=0,"Flying Tile aim-zero must select angle without a planar movement.");
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!actor.IsDead&&updates<200)Step(1);
            Step(2);FailIf(!actor.IsDead||moves==0,"Flying Tile must execute charge movement before its source tile collision/debris path.");
            _entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
