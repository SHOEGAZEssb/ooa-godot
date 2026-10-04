using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateScrollHandoffRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int hostCase1 = 0;
        foreach (bool retainedSword in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x34); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x2c, 0, 0);
            _player.WarpTo(new(6.75f, 64.25f));
            if (retainedSword)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                StepGameplayUpdates(17, Vector2.Zero, ["attack"], [], batched);
                FailIf(_player.SwordState != SwordActionState.Held, "Scroll retention fixture must hold its sword through actual input.");
            }
            OracleRandomState seed = _random.CaptureState();
            int loadedUnique = new ScreenTransitionGraphicsDatabase().ForTileset(_currentRoom.TilesetId).Unique;
            StepGameplayUpdates(1, Vector2.Left, retainedSword ? ["attack"] : [], [], batched);
            FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x33 || _saveData.MinimapRoom != 0x34,
                "The actual left exit must preload room 0:$33 while retaining the source minimap.");
            var swordState = _player.SwordState;
            int swordFrame = _player.SwordStateFrame;
            int unique = new ScreenTransitionGraphicsDatabase().ForTileset(_currentRoom.TilesetId).Unique;
            var scroll = new ScrollRom(false, 3, (int)(_player.PrecisePosition.X * 256), (int)(_player.PrecisePosition.Y * 256), unique, loadedUnique);
            if (retainedSword) { scroll[0xd200] = 0xe1; scroll[0xd201] = 5; }
            var enemies = new EnemyAiRom();
            var placement = new PlacementRom();
            placement[0xff94] = seed.Rng1; placement[0xff95] = seed.Rng2; placement.Generate();
            enemies[0xff94] = placement[0xff94]; enemies[0xff95] = placement[0xff95];
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            FailIf(slots.Count != 3 || slots.Keys.Any(e => e.Node is not (RiverZoraCharacter or BuzzBlobCharacter)),
                "Room 0:$33 must supply its original three enemies for the shared handoff fixture.");
            foreach (var (entity, slot) in slots)
            {
                int address = 0xd080 + slot * 0x100;
                enemies[address] = (byte)(((slot + 1) << 4) | 1);
                enemies[address + 1] = (byte)(entity.Node is RiverZoraCharacter ? 8 : 0x18);
                enemies.Word(address + 0x0a, (int)(entity.Node.Position.Y * 256));
                enemies.Word(address + 0x0c, (int)(entity.Node.Position.X * 256));
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                Vector2 point = new(x * 16 + 8, y * 16 + 8);
                enemies[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(point);
                enemies[0xce00 + y * 16 + x] = (byte)_currentRoom.GetTerrainInfo(point).Collision;
            }
            enemies[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            enemies[0xcd00] = 8; enemies.Update(); // Native state-zero preload.
            var observer = new ScrollEventObserver();
            FieldInfo eventField = typeof(RoomEventController).GetField("_eventsByPriority", flags)!;
            var originalEvents = (IRoomEvent[])eventField.GetValue(_roomEvents)!;
            eventField.SetValue(_roomEvents, originalEvents.Append(observer).ToArray());
            int resumed = 0;
            try
            {
                void Compare()
                {
                    bool frozen = scroll[0xcd04] != 2;
                    enemies[0xcd00] = scroll[0xcd00];
                    enemies[0xcc00] = (byte)_entities.FrameCounter;
                    enemies.Update(); // Object pass precedes the scroll handler.
                    if (frozen) scroll.Update(); else resumed++;
                    foreach (var (entity, slot) in slots)
                    {
                        int address = 0xd080 + slot * 0x100;
                        var enemy = (EnemyCharacter)entity.Node;
                        int state = enemy is RiverZoraCharacter z ? z.State : ((BuzzBlobCharacter)enemy).State;
                        int counter = enemy is RiverZoraCharacter rz ? rz.Counter : ((BuzzBlobCharacter)enemy).Counter;
                        int animation = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                        FailIf(state != enemies[address + 4] || counter != enemies[address + 6] || animation != enemies[address + 0x20] ||
                            enemy.Position != new Vector2(enemies.Word(address + 0x0c) / 256.0f, enemies.Word(address + 0x0a) / 256.0f),
                            $"ROM scroll handoff slot=${slot:x2}, frozen={frozen}, resumed={resumed}, batch={batched}: enemy updated at the wrong boundary.");
                    }
                    FailIf(observer.Updates != resumed, "Room-event dispatch must remain frozen through the final scroll update and resume afterward.");
                    FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) ||
                        _saveData.MinimapRoom != (scroll[0xcd04] == 2 ? 0x33 : 0x34), "Room identity/minimap handoff differs from ROM completion.");
                    if (frozen)
                        FailIf(_player.SwordState != swordState || _player.SwordStateFrame != swordFrame ||
                            retainedSword && scroll[0xd200] != 0xe1, "Scrolling must preserve the item parent without processing input or advancing its counter.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != enemies[0xff94] || random.Rng2 != enemies[0xff95] || random.Calls - seed.Calls != 256 + enemies.RandomCalls,
                        "Destination initialization/freeze/resume consumed RNG at a different boundary from the ROM.");
                }
                string[] held = retainedSword ? [] : ["attack"];
                StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, held, retainedSword ? [] : ["attack"], batched, Compare);
                FailIf(resumed != 0, "The finishing scroll update must still belong to the frozen object pass.");
                StepGameplayUpdates(3, Vector2.Zero, held, [], batched, Compare);
                FailIf(_player.IsAttacking || resumed != 3, "A released retained sword must end; a press consumed during scrolling must not replay after arrival.");
                StepGameplayUpdates(1, Vector2.Zero);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                FailIf(!_player.IsAttacking, "A fresh post-arrival input edge must start an item normally.");
            }
            finally { eventField.SetValue(_roomEvents, originalEvents); }
        }
        GD.Print("Validated actual room exits, ROM destination enemy initialization/freeze/resume and RNG, final-update event gating, retained items and consumed input edges.");
    }
}
