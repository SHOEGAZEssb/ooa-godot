using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateArmosBodyPublicationRom()
    {
        var cases=new System.Collections.Generic.List<(int,int,int,int,Vector2,int)>
        { (9,1,16,0x14,new(120,88),1), (9,3,16,0x14,new(120,255),3) };
        foreach(int state in new[]{11,12})
        foreach(Vector2 point in new[]{new Vector2(120,88),new Vector2(120,47),new Vector2(120,128),new Vector2(192,88),new Vector2(47,88)})
            cases.Add((state,3,8,state==12?0x78:0x28,point,3));
        foreach(int angle in new[]{0,8,16,24})
        {
            cases.Add((15,0,angle,0x50,new(120,88),100));
            cases.Add((16,0,angle,0x28,new(0,88),48));
        }
        foreach(var spec in cases)
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(4, 0x80, clearEntities: false);
            var spawner=_entities.Entities<ArmosWarriorActor>().Single();
            var body=spawner.CreateChild(1);var shield=spawner.CreateChild(2);var sword=spawner.CreateChild(3);
            _entities.Clear();AddChild(shield);AddChild(sword);
            body.InitializeState();shield.InitializeState();sword.InitializeState();
            F1Set(body,"_controlsDisabled",false);F1Set(shield,"<ShieldHits>k__BackingField",spec.Item1==12?2:3);sword.Position=new(120,56);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,152));_player.BeginCutsceneControl(owner:body);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var adapter=new ArmosWarriorRoomEntity(body,body.Entry);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var native=new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            var seed=_random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            native[0xcc08]=0xb4;native[0xcc0a]=0xb5;native[0xcc0c]=0xb6;
            const int at=0xd180;native[at]=1;native[at+1]=0x73;native[at+2]=1;native[at+11]=88;native[at+13]=120;
            native.Update(_entities.FrameCounter,_player.Position);
            native[at+0x31]=0xd2;native[at+0x32]=0xd3;native[0xd2b2]=(byte)(spec.Item1==12?2:3);native[0xd38b]=56;native[0xd38d]=120;
            Vector2 initial=spec.Item5+(fractional?new Vector2(.25f,.5f):Vector2.Zero);body.Position=initial;
            F1Set(body,"<State>k__BackingField",spec.Item1);F1Set(body,"<Substate>k__BackingField",spec.Item1==9?4:0);
            F1Set(body,"<Counter>k__BackingField",spec.Item2);F1Set(body,"<Angle>k__BackingField",spec.Item3);
            F1Set(body,"<Speed>k__BackingField",spec.Item4);F1Set(body,"<Turn>k__BackingField",8);
            F1Set(body,"_zFixed",spec.Item1==16?-0x280:0);F1Set(body,"_speedZ",spec.Item1==16?-0x180:0);
            native[at+4]=(byte)spec.Item1;native[at+5]=(byte)(spec.Item1==9?4:0);native[at+6]=(byte)spec.Item2;
            native[at+9]=(byte)spec.Item3;native[at+16]=(byte)spec.Item4;native[at+0x30]=8;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native.Word(at+14,spec.Item1==16?-0x280:0);native.Word(at+20,spec.Item1==16?-0x180:0);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                FailIf(body.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||body.State!=native[at+4]||
                    body.Substate!=native[at+5]||body.Counter!=native[at+6]||body.Angle!=native[at+9]||body.Speed!=native[at+16]||
                    (ushort)body.ZFixed!=native.Word(at+14)||(ushort)SomariaPrivate<int>(body,"_speedZ")!=native.Word(at+20)||
                    observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Armos$73:$01 state${spec.Item1:x2}/angle${spec.Item3:x2}/fraction{fractional}/update{updates}: original body movement/counter/air/scratch differs.");
                observedCapture.Samples=0;
            });
            _dialogue.ShowMessage("Armos body hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            int first=Math.Min(2,spec.Item6);Step(first);
            _dialogue.ShowMessage("Armos active motion hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            Step(spec.Item6-first);Step(2);
            FailIf(spec.Item1 is 15 or 16 && (body.State!=14||body.ZFixed!=0||body.Counter>=60),
                "Armos charge/recoil must land, enter the source wait and retain its post-completion no-motion updates.");
            _entities.Clear();shield.Free();sword.Free();_player.EndCutsceneControl(body);
        }
    }
}
