using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCompanionPlacementCapacityRom()
    {
        // Execute the original room parser, then the actual destination
        // preload/lifetime boundary. Occupants declare retained capacity;
        // their unrelated handlers are outside this focused comparison.
        foreach (int room in new[] {0x5c,0x89})
        foreach (int free in new[] {0,1,2})
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x33); _entities.Clear();
            _saveData.SetRoomFlag(0,0x5c,0x80,false);
            var native = new PlacementRom();
            var occupants = new List<InteractionSlotValidationEntity>();
            for (int slot = 2; slot < 16; slot++)
            {
                var puff = new PuzzlePuffEffect(); puff.Initialize(new(24,24),SoundId.MusNone);
                var occupant = new InteractionSlotValidationEntity(puff, _ => { });
                _entities.AddEntity(occupant); occupants.Add(occupant);
                FailIf(_entities.InteractionSlot(puff) != slot,"Capacity fixture must fill native slots$d2-$df in order.");
                native[(0xd0+slot)*256+0x40] = 2;
                native[(0xd0+slot)*256+0x41] = 5;
            }
            // Nonadjacent holes prove first-free allocation, rather than a
            // room-specific offset or a reserved placeholder for a missing row.
            if (free >= 1) { occupants[0].Finished = true; native[0xd240] = 0; }
            if (free == 2) { occupants[^1].Finished = true; native[0xdf40] = 0; }
            var seed = _random.CaptureState();
            native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            native[0xcc2d] = 0; native[0xcc30] = (byte)room;
            native[0xcd00] = 8; native[0xcd02] = 2;
            native.ParseRoom();
            var destination = _world.LoadRoom(0,room);
            _entities.BeginScreenTransition(0,destination,new(0,128),_player);
            var gameplay = new SomariaRom(_saveData,_random.CaptureState(),destination,0,24,24);
            gameplay[0xcd00] = 8;
            for (int page = 0xd2; page < 0xe0; page++)
            for (int offset = 0x40; offset < 0x80; offset++)
                gameplay[page*256+offset] = native[page*256+offset];
            gameplay.Update(0,0,_entities.FrameCounter);
            int IncomingCount() => _entities.EntityAdapters<KeyholeControllerRoomEntity>().Count() +
                _entities.Entities<CompanionBarrierRoomEntity>().Count + _entities.Entities<CompanionTutorialRoomEntity>().Count;
            FailIf(IncomingCount() != free,"Native full/one/two-free parser must admit only available leading source placements.");
            var random = _random.CaptureState();
            FailIf(random.Rng1 != native[0xff94] || random.Rng2 != native[0xff95] ||
                random.Calls-seed.Calls != native.RandomCalls || native.RandomCalls != 256,
                "Interaction placement capacity must retain the room parser's exact256 shared RNG calls.");
            void Audit()
            {
                FailIf(IncomingCount() != free,"Scroll lifetime must not retry source rows skipped by the full interaction pool.");
                foreach (var actor in _entities.Entities<CompanionBarrierRoomEntity>())
                {
                    int slot = 0xd000+_entities.InteractionSlot(actor)*256+0x40;
                    FailIf(gameplay[slot] != 1 || gameplay[slot+1] != 0x71 ||
                        gameplay[slot+2] != actor.Record.SubId || actor.State != gameplay[slot+4],
                        "Barrier allocation/preload must match the native source row and first-free slot.");
                }
                foreach (var actor in _entities.Entities<CompanionTutorialRoomEntity>())
                {
                    int slot = 0xd000+_entities.InteractionSlot(actor)*256+0x40;
                    FailIf(gameplay[slot] != 1 || gameplay[slot+1] != 0xd0 ||
                        gameplay[slot+2] != actor.Record.SubId || actor.State != gameplay[slot+4],
                        "Tutorial allocation/preload must match the native source row and first-free slot.");
                }
                foreach (var actor in _entities.EntityAdapters<KeyholeControllerRoomEntity>())
                {
                    int slot = 0xd000+_entities.InteractionSlot(actor.Node)*256+0x40;
                    FailIf(gameplay[slot] != 0x81 || gameplay[slot+1] != 0xdc || !actor.Initialized || gameplay[slot+4] != 1,
                        $"Graveyard controller must follow its real barrier even through nonadjacent free slots: free={free}, slot=${slot:x4}, native=${gameplay[slot]:x2}/${gameplay[slot+1]:x2}/${gameplay[slot+4]:x2}, runtime={actor.Initialized}/{actor.Finished}.");
                }
            }
            Audit();
            void Step() => StepGameplayUpdates(2,Vector2.Zero,batched:free == 2,afterUpdate:() => {
                gameplay.Update(0,0,_entities.FrameCounter); Audit();
            });
            Step();
            _entities.FinishScreenTransition();
            for (int page = 0xd2; page < 0xe0; page++)
                if (native[page*256+0x40] == 2) gameplay[page*256+0x40] = 0;
            gameplay[0xcd00] = 1;
            Step();
        }
    }
}
