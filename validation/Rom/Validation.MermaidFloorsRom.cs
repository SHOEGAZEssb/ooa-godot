using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidFloorsRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Feather, 0);
            _inventory.EquipA(ItemId.Feather); _inventory.EquipB(0);
            LoadValidationRoom(5, 0x3f);
            var records = new MermaidDungeonDatabase().GetRoomRecords(5, 0x3f);
            FailIf(records.Count != 2 || records[0] is not {Order:2, Id:0x22, SubId:0, Y:0x58, X:0x78} ||
                records[1] is not {Order:3, Id:0x15, SubId:0},
                "Source room$5:$3f requires floor-color/toggle controllers in orders2/3.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var actor in active.Where(actor => actor is not FloorColorChangerRoomEntity and not ToggleFloorRoomEntity).ToArray())
            { active.Remove(actor); free.Invoke(_entities, [actor]); }
            var changer = _entities.Entities<FloorColorChangerRoomEntity>().Single();
            var toggle = _entities.Entities<ToggleFloorRoomEntity>().Single();
            // Source layout: red controller$57, surrounding red floor$9d.
            FailIf(_currentRoom.Layout[0x57] != 0xad || _currentRoom.Layout[0x67] != 0x9d,
                "Mermaid colored floor lost its original control and southern approach tiles.");
            _player.WarpTo(new(120, 104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Feather takeoff must use the actual southern floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 104);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int changerSlot = 0xd040+_entities.InteractionSlot(changer)*256;
            int toggleSlot = 0xd040+_entities.InteractionSlot(toggle)*256;
            rom[changerSlot]=1; rom[changerSlot+1]=0x22;
            rom[changerSlot+0xb]=0x58; rom[changerSlot+0xd]=0x78;
            rom[toggleSlot]=1; rom[toggleSlot+1]=0x15;
            var sounds = _sound.AttachPlayRequestAudit();
            int update=0;
            void Step(int count=1, int angle=0xff, bool jump=false)
            {
                int tick=0;
                int keys=angle switch {0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0};
                StepGameplayUpdates(count, angle==0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    jump ? ["attack"] : [], jump ? ["attack"] : [], batch, () =>
                    {
                        rom.UpdateGameplay(jump && tick++==0 ? 1 : 0, keys|(jump ? 1 : 0), angle, _entities.FrameCounter);
                        rom.AdvanceTileGraphics();
                        string context=$"Mermaid floor$5:$3f, batch={batch}, update{++update}";
                        CompareSomariaMotionRom(rom, context);
                        FailIf(SomariaPrivate<int>(changer,"_lastControlTile") != rom[changerSlot+3] ||
                            SomariaPrivate<int>(toggle,"_lastTilePosition") != rom[toggleSlot+0x30], context+": parent latches differ.");
                        var workers=_entities.Entities<FloorColorWorkerRoomEntity>();
                        int nativeWorkers=Enumerable.Range(0xd2,14).Count(page =>
                            rom[page*256+0x40]!=0 && rom[page*256+0x41]==0x22 && rom[page*256+0x42]==1);
                        FailIf(workers.Count!=nativeWorkers, context+": worker allocation differs.");
                        foreach (var worker in workers)
                        {
                            int slot=0xd040+_entities.InteractionSlot(worker)*256;
                            FailIf(worker.Index!=rom[slot+6],context+": conversion counter differs.");
                        }
                        var random=_random.CaptureState();
                        FailIf(random.Rng1!=rom[0xff94] || random.Rng2!=rom[0xff95] ||
                            random.Calls-seed.Calls!=rom.RandomCalls ||
                            !sounds.Requests.Where(cue=>cue!=SoundId.SndText).SequenceEqual(rom.Sounds),
                            context+": shared RNG or ordered cues differ.");
                    });
            }
            Step();
            for (int color=0; color<2; color++)
            {
                Step(angle:0,jump:true); Step(14,0); Step(4,16);
                for (int wait=0; _player.TopDownAirborne && wait<80; wait++) Step();
                FailIf(_player.TopDownAirborne || _currentRoom.Layout[0x57] != 0xae+color,
                    "An actual Feather landing must cycle the original Mermaid control tile.");
                Step(66);
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count!=0 || rom.RandomCalls!=256*(color+1),
                    "Each changed Mermaid control color must finish one 256-draw conversion worker.");
                for (int cell=0x11; cell<0x9f; cell++)
                    if (cell!=0x57 && (cell&15)!=0 && (cell&0xf0)!=0)
                        FailIf(_currentRoom.Layout[cell] is >=0x9d and <=0x9f && _currentRoom.Layout[cell]!=0x9e+color,
                            $"Mermaid colored floor${cell:x2} did not finish its source conversion.");
                if (color==0) Step(20,16);
            }
        }
        GD.Print("Validated placed Mermaid floor controllers, repeated actual Feather crossings, native conversion, ordered cues and shared RNG in split/batched gameplay.");
    }
}
