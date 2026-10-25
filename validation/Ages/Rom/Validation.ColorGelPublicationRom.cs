using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateColorGelPublicationRom()
    {
        Vector2I[] far=[new(-16,-16),new(0,-16),new(16,-16),new(-16,0),new(16,0),new(-16,16),new(0,16),new(16,16)];
        var offsets=far.Concat(from y in Enumerable.Range(-2,5) from x in Enumerable.Range(-2,5) select new Vector2I(x,y));
        foreach(var offset in offsets)
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(4, 0x71);
            FillValidationRoom(0xad);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,24));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var data=EnemyDatabase.Shared;var actor=new ColorChangingGelCharacter();
            actor.Initialize(data.ImportedEnemy(0x47,0),_currentRoom,new(120,88),_random,data.ColorChangingGelPalettes,_sound.PlaySound);
            var adapter=new ColorChangingGelRoomEntity(actor,
                new EnemyCombatSourceDescriptor(0x47,0,0x6e,0,1,EnemyHandlerKind.ColorChangingGel,"colorChangingGel.s:enemyCode47"),_sound.PlaySound);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,moves=0;
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0x97;
            const int at=0xd180;native[at]=1;native[at+1]=0x47;native[at+11]=88;native[at+13]=120;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int calls=native.MovementCalls;native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;if(native.MovementCalls!=calls)moves++;
                FailIf((int)actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.ColorCounter!=native[at+7]||
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (ushort)SomariaPrivate<int>(actor,"_zFixed")!=native.Word(at+14)||(ushort)SomariaPrivate<int>(actor,"_speedZ")!=native.Word(at+20)||
                    actor.Color!=(native[at+0x1b]&7)||actor.CollisionMode!=native[at+0x25]||actor.StoredTile!=native[at+0x32]||
                    observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Color Gel$47 target{offset}/fraction{fractional}/update{updates}: original air/near-target/landing/color state or scratch differs.");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "Color Gel must preserve the common initialization draw and avoid new RNG during declared hopping.");observedCapture.Samples=0;
            });
            Step(1);
            Vector2 initial=new(120+(fractional?.25f:0),88+(fractional?.5f:0));actor.Position=initial;
            F1Set(actor,"_state",ColorChangingGelState.Hopping);F1Set(actor,"_counter",5);
            F1Set(actor,"_zFixed",-0x280);F1Set(actor,"_speedZ",-0x180);F1Set(actor,"_target",new Vector2(120+offset.X,88+offset.Y));
            native[at+4]=10;native[at+6]=5;native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native.Word(at+14,-0x280);native.Word(at+20,-0x180);native[at+0x30]=(byte)(88+offset.Y);native[at+0x31]=(byte)(120+offset.X);
            Step(3);_dialogue.ShowMessage("Color Gel air hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(actor.State==ColorChangingGelState.Hopping&&updates<80)Step(1);
            Step(3);
            bool near=Math.Abs(offset.X)<=1&&Math.Abs(offset.Y)<=1;
            FailIf(actor.State!=ColorChangingGelState.Waiting||actor.Counter!=147||actor.Position!=actor.Position.Floor()||
                (((int)actor.Position.X&15)!=8)||(((int)actor.Position.Y&15)!=8)||near&&moves!=0||!near&&moves==0,
                "Color Gel must retain source near-box no-motion, clear both fractional bytes at landing and resume the waiting counter.");
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
