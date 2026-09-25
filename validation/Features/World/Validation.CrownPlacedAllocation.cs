using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPlacedAllocation()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var parts=(Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            _saveData.SetRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(24,24));
            _entities.Clear();
            for(int slot=2;slot<16;slot++)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            // mainData.s:4:bc places INTERAC$21:$17 then four PART$09
            // records. The full interaction pool must not consume PART slots.
            FailIf(_entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0 ||
                _entities.Entities<GroundButtonRoomEntity>().Count!=4,
                "Full INTERACTION pool must skip Crown's chest controller while still allocating its four buttons.");
            Step(20);
            FailIf(_entities.OutgoingEntities<PuzzlePuffEffect>().Count!=0 ||
                _entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0,
                "Freeing outgoing INTERACTION slots must not retry a skipped placed chest controller.");
            _entities.FinishScreenTransition();
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            FailIf(_entities.Entities<RetractableTriggerChestRoomEntity>().Count!=1,
                "A later room parse with a free INTERACTION slot must create the chest controller.");
            _entities.FinishScreenTransition();

            // mainData.s4:a6 order: translator, shutter, two PART reflectors,
            // then the torch-scanner pointer. Test the allocation boundary
            // without advancing a dungeon route or solving the torch puzzle.
            for(int free=0;free<=3;free++)
            {
                LoadValidationRoom(4,0xa6);
                _player.WarpTo(new(56,88));
                _entities.Clear();
                for(int occupied=0;occupied<14-free;occupied++)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
                _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
                FailIf(_entities.Entities<TorchTriggerTranslatorRoomEntity>().Count!=(free>=1?1:0) ||
                    _entities.Entities<DungeonDoorRoomEntity>().Count!=(free>=2?1:0) ||
                    _entities.Entities<LightableTorchRoomEntity>().Count!=(free>=3?4:0) ||
                    _entities.Entities<RotatableSeedThingRoomEntity>().Count!=2,
                    $"Room4:a6 must allocate translator/shutter/scanner in source order with {free} free INTERACTION slots, independently of PART reflectors.");
                Step(20);
                FailIf(_entities.Entities<TorchTriggerTranslatorRoomEntity>().Count!=(free>=1?1:0) ||
                    _entities.Entities<DungeonDoorRoomEntity>().Count!=(free>=2?1:0) ||
                    _entities.Entities<LightableTorchRoomEntity>().Count!=(free>=3?4:0),
                    "Deleted outgoing puffs must not cause skipped torch-room interactions to reappear.");
                _entities.FinishScreenTransition();
            }

            foreach(int room in new[]{0xbc,0xa6,0xba,0x9b})
            {
                LoadValidationRoom(4,room);
                _player.WarpTo(room==0xbc?new(24,24):new(56,88));
                _entities.Clear();
                var reservations=new List<IRoomEntity>();
                try
                {
                    for(int slot=0;slot<16;slot++)
                    {
                        var reservation=new ArmosSlotReservation();
                        reservations.Add(reservation);
                        parts.Add(reservation,slot);
                    }
                    _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
                    FailIf(_entities.Entities<GroundButtonRoomEntity>().Count!=0 ||
                        _entities.Entities<RotatableSeedThingRoomEntity>().Count!=0 ||
                        _entities.Entities<SeedShooterEyeStatueRoomEntity>().Count!=0 ||
                        _entities.Entities<OwlStatueRoomEntity>().Count!=0,
                        "Full PART allocation must skip placed buttons, timed reflectors and eye statues.");
                    if(room==0xbc)
                        FailIf(_entities.Entities<RetractableTriggerChestRoomEntity>().Count!=1,
                            "A full PART pool must not prevent the independent INTERACTION allocation.");
                    parts.Remove(reservations[0]);
                    Step(2);
                    FailIf(_entities.Entities<GroundButtonRoomEntity>().Count!=0 ||
                        _entities.Entities<RotatableSeedThingRoomEntity>().Count!=0 ||
                        _entities.Entities<SeedShooterEyeStatueRoomEntity>().Count!=0 ||
                        _entities.Entities<OwlStatueRoomEntity>().Count!=0,
                        "A newly freed PART slot must not retry records skipped during parsing.");
                    _entities.FinishScreenTransition();
                }
                finally
                {
                    foreach(var reservation in reservations)
                    {
                        parts.Remove(reservation);
                        reservation.Node.Free();
                    }
                }
                LoadValidationRoom(4,room);
                FailIf(room switch
                    {
                        0xbc => _entities.Entities<GroundButtonRoomEntity>().Count!=4,
                        0xa6 => _entities.Entities<RotatableSeedThingRoomEntity>().Count!=2,
                        0x9b => _entities.Entities<OwlStatueRoomEntity>().Count!=1,
                        _ => _entities.Entities<SeedShooterEyeStatueRoomEntity>().Count!=3
                    },
                    "A later room parse must create previously skipped PART records when capacity is available.");
            }
            bool HasPuzzleController() => _entities.EntityAdapters<IRoomEntity>().Any(entity=>entity is
                PushBlockSynchronizerRoomEntity or WallSquishRoomEntity or ButtonBridgeRoomEntity or PuzzleTrapResetRoomEntity
                or DungeonPatternHintRoomEntity or DungeonPuzzleChestRoomEntity or DungeonTriggerChestScriptRoomEntity
                or MovingSideScrollPlatformRoomEntity or SmogEncounterRoomEntity or DungeonRewardRoomEntity);
            for(int free=0;free<=4;free++)
            {
                LoadValidationRoom(4,0xbb);
                _player.WarpTo(new(120,120));
                _entities.Clear();
                for(int occupied=0;occupied<14-free;occupied++)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
                _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
                // Source order is $12:$00, $e2:$01, $7e:$00. Parsing
                // allocates all three before state0 can delete the entry.
                // The scanner reuses that hole but retains its own slot
                // while attempting children in descending tile order.
                int expected=free<2?0:free==2?1:free-2;
                FailIf(_entities.Entities<StatueEyeball>().Count!=expected,
                    $"Crown entrance with {free} free INTERACTION slots must create {expected} statue eyes after entry state0 deletes.");
                FailIf(_entities.EntityAdapters<DungeonEntranceRoomEntity>().Any() ||
                    _entities.EntityAdapters<StatueEyeballSpawnerRoomEntity>().Any(),
                    "Entry and eye scanner must release their native slots during ordinary scrolling preload.");
                Step(20);
                FailIf(_entities.Entities<StatueEyeball>().Count!=expected,
                    "The entrance scanner must not retry failed children after outgoing puffs delete.");
                _entities.FinishScreenTransition();
            }
            foreach(bool collected in new[]{false,true})
            for(int free=0;free<=2;free++)
            {
                _saveData.SetRoomFlag(4,0xb8,OracleSaveData.RoomFlagItem,collected);
                LoadValidationRoom(4,0xb8);
                _player.WarpTo(new(120,120));
                _entities.Clear();
                for(int occupied=0;occupied<14-free;occupied++)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
                _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
                // $7f:$00 allocates its pedestal before checking ROOMFLAG_ITEM.
                // The glow uses reserved $d1, independently of dynamic capacity.
                FailIf(_entities.Entities<DungeonEssence>().Count!=(free>0?1:0) ||
                    _entities.Entities<DungeonEssencePedestal>().Count!=(free==2?1:0) ||
                    _entities.Entities<DungeonEssenceGlow>().Count!=(!collected && free>0?1:0),
                    $"Crown essence allocation order differs with {free} free slots and collected={collected}: parent={_entities.Entities<DungeonEssence>().Count}, pedestal={_entities.Entities<DungeonEssencePedestal>().Count}, glow={_entities.Entities<DungeonEssenceGlow>().Count}.");
                Step(20);
                FailIf(_entities.Entities<DungeonEssencePedestal>().Count!=(free==2?1:0),
                    "Essence state0 must not retry its failed pedestal after outgoing slots are freed.");
                _entities.FinishScreenTransition();
            }
            _saveData.SetRoomFlag(4,0xb8,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xb8);
            Step(2);
            FailIf(_entities.Entities<DungeonEssenceGlow>().Count!=1,
                "The uncollected Crown Essence must own reserved INTERACTION$d1.");
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            FailIf(_entities.Entities<DungeonEssenceGlow>().Count!=1 ||
                _entities.OutgoingEntities<DungeonEssenceGlow>().Count!=0,
                "Creating a new Essence glow must overwrite outgoing reserved INTERACTION$d1.");
            _entities.FinishScreenTransition();
            for(int free=0;free<=4;free++)
            {
                LoadValidationRoom(0,0x0a);
                _entities.Clear();
                for(int occupied=0;occupied<14-free;occupied++)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
                var entrance=_roomEvents.Get<CrownDungeonEntranceEvent>();
                _sound.ClearPlayRequestAudit();
                // Exercise only scriptHelp's drawing/allocation helper.
                // It attempts X=$60/$70/$80/$90 independently and keeps
                // the already drawn facade/sound when allocation fails.
                entrance.RunNativeHandler("Frame1");
                var puffs=_entities.Entities<PuzzlePuffEffect>().Where(p=>p.Flickers).ToArray();
                FailIf(puffs.Length!=free ||
                    !puffs.Select(p=>p.Position).SequenceEqual(Enumerable.Range(0,free).Select(i=>new Vector2(0x60+i*0x10,0x20))) ||
                    puffs.Any(p=>p.ElapsedUpdates!=1 || !p.Visible) ||
                    entrance.Phase!=1 || _currentRoom.GetBackgroundSubtileForValidation(12,3)!=0x4d ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose)!=1,
                    $"Crown opening helper lost ordered partial allocation or non-allocation effects with {free} slots free.");
                Step(20);
                FailIf(_entities.Entities<PuzzlePuffEffect>().Count!=0,
                    "Crown opening must not retry failed puff allocations on later updates.");
                entrance.RunNativeHandler("Frame2");
                FailIf(_entities.Entities<PuzzlePuffEffect>().Count(p=>p.Flickers)!=4,
                    "A later Crown opening helper must allocate after the earlier effects release their slots.");
            }
            foreach(int room in new[]{0x9b,0x9e,0xa5,0xad,0xba,0x95,0x96,0x97,0xb4,0xbf})
            {
                _saveData.SetRoomFlag(4,room,OracleSaveData.RoomFlagItem,false);
                LoadValidationRoom(4,room);
                FailIf(!HasPuzzleController(),$"Room4:{room:x2} must contain the source controller tested by the full-pool fixture.");
                _entities.Clear();
                for(int slot=2;slot<16;slot++)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
                _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
                FailIf(HasPuzzleController(),$"Room4:{room:x2} allocated a puzzle controller in a full INTERACTION pool.");
                Step(20);
                FailIf(HasPuzzleController(),$"Room4:{room:x2} retried a skipped puzzle controller after slots became free.");
                _entities.FinishScreenTransition();
                LoadValidationRoom(4,room);
                FailIf(!HasPuzzleController(),$"Room4:{room:x2} did not allocate its controller on the next parse with free slots.");
            }
            LoadValidationRoom(0,0x60);
        }
    }
}
