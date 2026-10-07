using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidSpinnerRom()
    {
        foreach (bool batch in new[] { false,true })
        foreach (int roomId in new[] { 0x18,0x39 })
        {
            ReinitializeGameplayForValidation();
            byte initial=(byte)(roomId == 0x18 ? 0xa1 : 0xa0);
            _runtimeState.SetWramByte(OracleRuntimeState.SpinnerStateAddress,initial);
            LoadValidationRoom(5,roomId);
            var spinners=_entities.Entities<DungeonSpinnerRoomEntity>().OrderBy(actor => actor.Position.X).ToArray();
            FailIf(spinners.Length != (roomId == 0x18 ? 2 : 1),"Mermaid direct spinner count must match original room$5:$18/$39.");
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var actor in active.Where(actor => actor is not DungeonSpinnerRoomEntity).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            int x=roomId == 0x18 ? 72 : 136, y=roomId == 0x18 ? 40 : 72;
            _player.WarpTo(new(x,y)); _player.Face(Vector2I.Down); _inventory.EquipA(0); _inventory.EquipB(0);
            FailIf(_collision.Collides(_player.Position),"Mermaid spinner must start on original reachable north floor.");
            byte[] positions=roomId == 0x18 ? [0x44,0x78] : [0x68];
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,2,x,y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcdd4]=initial;
            rom[0xcc39]=(byte)_rooms.CurrentDungeonIndex;
            for (int index=0;index<spinners.Length;index++)
            {
                FailIf(_entities.InteractionSlot(spinners[index]) != index+2,"Direct spinners must reserve source main-stream slots before their arrows.");
                int a=0xd240+index*256;
                rom[a]=1; rom[a+1]=0x7d; rom[a+2]=(byte)index; rom[a+11]=positions[index]; rom[a+13]=(byte)(1<<index);
            }
            var sounds=_sound.AttachPlayRequestAudit(); int update=0;
            void Step(int count=1,int angle=0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,angle switch { 0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0 },angle,_entities.FrameCounter);
                rom.CreateMenuView().UpdateScreenShake(); rom.AdvanceTileGraphics();
                string context=$"Mermaid spinner5:${roomId:x2}, batch={batch}, update{++update}";
                CompareSomariaMotionRom(rom,context);
                foreach (var spinner in spinners)
                {
                    int a=0xd040+_entities.InteractionSlot(spinner)*256;
                    int nativePhase=rom[a+4];
                    if (nativePhase == 1 && (rom[0xcc95]&0x80) != 0 &&
                        spinner.Phase == SpinnerPhase.Touched) nativePhase=2;
                    int phase=spinner.Phase switch { SpinnerPhase.Waiting=>1,SpinnerPhase.Touched=>2,SpinnerPhase.Turning=>3,SpinnerPhase.Exiting=>4,_=>-1 };
                    var animation=SomariaPrivate<EnemyAnimationPlayer>(spinner,"_spinnerAnimation");
                    FailIf(phase != nativePhase || spinner.Red != (rom[a+9] != 0) ||
                        phase == 1 && spinner.WaitCounter != rom[a+6] || phase == 4 && spinner.ExitCounter != rom[a+6] ||
                        animation.CurrentParameter != rom[a+0x21] || SomariaPrivate<int>(animation,"_frameCounter") != rom[a+0x20],
                        context+$": physical spinner${a:x4} phase/color/clock/animation differs.");
                }
                var arrows=_entities.Entities<DungeonSpinnerArrowRoomEntity>();
                int nativeArrows=Enumerable.Range(0xd2,14).Count(page => rom[page*256+0x40] != 0 &&
                    rom[page*256+0x41] == 0x7d && rom[page*256+0x42] == 2);
                FailIf(arrows.Count != nativeArrows,context+": checked arrow allocation differs.");
                foreach (var arrow in arrows)
                {
                    int a=0xd040+_entities.InteractionSlot(arrow)*256;
                    FailIf(rom[a+2] != 2 || arrow.Position != new Vector2(rom[a+13],rom[a+11]) ||
                        arrow.Visible != ((rom[a+0x1a]&0x80) != 0),context+": physical arrow position/visibility differs.");
                }
                var rng=_random.CaptureState();
                FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) != rom[0xcdd4] ||
                    rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                    _entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),context+": shared spinner bits/RNG/shake/audio differ.");
            });
            void Turn(DungeonSpinnerRoomEntity spinner,int approach,byte wanted)
            {
                for (int walk=0;spinner.Phase == SpinnerPhase.Waiting && walk<40;walk++)
                    Step(angle:approach);
                FailIf(spinner.Phase != SpinnerPhase.Touched,"Actual floor approach must reach Mermaid spinner.");
                for (int wait=0;spinner.Phase != SpinnerPhase.Waiting && wait<80;wait++) Step();
                FailIf(spinner.Phase != SpinnerPhase.Waiting || rom[0xcdd4] != wanted,"Mermaid completed turn must XOR only its own mask.");
                Step(33);
            }
            Step(33);
            if (roomId == 0x18)
            {
                FailIf(!spinners[0].Red || spinners[1].Red,"Raw subid$01 must be replaced by live mask$02, independently of mask$01.");
                Turn(spinners[0],16,0xa0);
                // The forced exit ends near the corridor's west edge. Move
                // to its center before walking beside the lower wall.
                for (int walk=0;_player.PrecisePosition.X<104 && walk<10;walk++) Step(angle:8);
                for (int walk=0;_player.PrecisePosition.Y<119 && walk<40;walk++)
                { Step(angle:16); FailIf(_collision.Collides(_player.Position),$"The corridor between Mermaid spinners must be reachable: Link={_player.PrecisePosition}, tile=${_currentRoom.GetMetatile(_player.Position):x2}, collision=${_currentRoom.GetTerrainInfo(_player.Position).Collision:x2}."); }
                Turn(spinners[1],8,0xa2);
                Turn(spinners[1],0,0xa0);
            }
            else { Turn(spinners[0],16,0xa1); Turn(spinners[0],8,0xa0); }
        }
        GD.Print("Validated clean-US direct Mermaid spinner placement/slots, independent shared masks, actual corridor approaches, turns/arrows/forced exits and repeat through split/batched gameplay.");
    }
}
