using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceScrollPreloadRom()
    {
        ValidatePossessedNayruScrollPreloadRom();
        // bank0.updateInteractions admits state 0 under wScrollMode=$08,
        // then freezes state 1. The invisible $6b:$02 loads its script but
        // does not run it until ordinary gameplay resumes.
        foreach (bool mystery in new[] { false, true })
        foreach (bool bombs in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            if (mystery) _inventory.GiveTreasure(TreasureId.MysterySeeds, 0);
            if (bombs) _saveData.SetRoomFlag(0, 0x83, 0x80);
            LoadValidationRoom(1, 0x36);
            _player.WarpTo(new(0x50, 0x7f));
            FailIf(_collision.Collides(_player.Position), "Palace $1:$36 south approach is solid.");
            _transitions.BeginScroll(_player, Vector2I.Down, 0x46);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 2, 0x50, 0x7f);
            const int trigger = 0xd240;
            rom[trigger] = 1; rom[trigger + 1] = 0x6b; rom[trigger + 2] = 2;
            rom[trigger + 11] = 0x38; rom[trigger + 13] = 0x50;
            rom[0xcd00] = 8;
            rom.AdvanceInteractions(0);
            void Compare()
            {
                var incoming = _entities.EntityAdapters<PalaceApproachRoomEntity>().Where(actor => !actor.IsRalph).ToArray();
                FailIf(incoming.Length != (rom[trigger] == 0 ? 0 : 1) || incoming.Any(actor => actor.Node.Visible) ||
                    (rom[trigger] != 0 && rom[trigger + 4] != 1) ||
                    _entities.EntityAdapters<PalaceApproachRoomEntity>().Any(actor => actor.IsRalph) ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 || _dialogue.IsOpen,
                    $"Palace $6b:$02 scroll initialization/freeze differs: Mystery={mystery}, Bombs={bombs}, batch={batch}.");
            }
            Compare();
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batch, afterUpdate: () =>
            {
                rom.AdvanceInteractions(1);
                Compare();
            });
            FailIf(_transitions.ScrollActive || _rooms.CurrentRoom.Id != 0x46,
                "Palace entrance scroll did not finish.");
            rom[0xcd00] = 1;
            rom.Word(0xd00a, (int)(_player.Position.Y * 256));
            rom.Word(0xd00c, (int)(_player.Position.X * 256));
            rom.AdvanceInteractions(2);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            Compare();
        }

        // Source placements $be:$00-$03. These rooms also preload ENEMY$54
        // and its invisible PART$0e; initialization must preserve RNG/order.
        int[] targets = [0x15, 0x17, 0x35, 0x37];
        Vector2[] buttons = [new(0x48, 0x38), new(0x88, 0x18), new(0x88, 0x58), new(0x78, 0x58)];
        for (int index = 0; index < targets.Length; index++)
        foreach (bool completed in new[] { false, true })
        foreach (bool batch in RomHostSchedules(index))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            if (completed) _saveData.SetRoomFlag(1, targets[index], 0x80);
            int source = index % 2 == 0 ? 0x25 : 0x27;
            var direction = index < 2 ? Vector2I.Up : Vector2I.Down;
            LoadValidationRoom(1, source);
            int edgeY = index < 2 ? 0 : 0x7f;
            int edgeX = Enumerable.Range(8, 144).First(x => !_collision.Collides(new(x, edgeY)));
            _player.WarpTo(new(edgeX, edgeY));
            var seed = _random.CaptureState();
            _transitions.BeginScroll(_player, direction, targets[index]);
            var guards = _entities.EntityAdapters<AmbiGuardRoomEntity>().ToArray();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, edgeX, edgeY);
            rom[0xcd00] = 8; rom[0xcc08] = 0x4d;
            rom.GeneratePlacementBuffer();
            foreach (var guard in guards)
            {
                int address = 0xd080 + guard.Slot * 256;
                rom[address] = 0x11; rom[address + 1] = 0x54; rom[address + 2] = (byte)guard.Actor.SubId;
                rom.Word(address + 10, (int)(guard.Actor.Position.Y * 256));
                rom.Word(address + 12, (int)(guard.Actor.Position.X * 256));
            }
            const int buttonSlot = 0xd240;
            rom[buttonSlot] = 1; rom[buttonSlot + 1] = 0xbe; rom[buttonSlot + 2] = (byte)index;
            rom[buttonSlot + 11] = (byte)buttons[index].Y; rom[buttonSlot + 13] = (byte)buttons[index].X;
            rom.AdvanceEnemies(0); rom.AdvanceParts(0); rom.AdvanceInteractions(0);
            void Compare()
            {
                var incoming = _entities.EntityAdapters<PalaceButtonRoomEntity>().ToArray();
                FailIf(incoming.Length != (rom[buttonSlot] == 0 ? 0 : 1) ||
                    incoming.Any(button => button.State != rom[buttonSlot + 4] || button.Counter != rom[buttonSlot + 6] || button.Node.Visible),
                    $"Palace button ${index:x2} scroll initialization/freeze differs, completed={completed}, batch={batch}.");
                foreach (var guard in guards)
                {
                    int address = 0xd080 + guard.Slot * 256;
                    FailIf(!guard.Node.Visible || guard.Actor.State != rom[address + 4] ||
                        guard.Actor.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        guard.Actor.Counter != rom[address + 6] || guard.Actor.Angle != rom[address + 9],
                        $"Palace ENEMY$54:${guard.Actor.SubId:x2} advanced or lost presentation during scrolling.");
                }
                var detectors = _entities.EntityAdapters<DetectionHelperRoomEntity>().ToArray();
                var random = _random.CaptureState();
                FailIf(detectors.Length != guards.Length || detectors.Any(part => part.State != 1 || part.Node.Visible) ||
                    random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95],
                    "Palace scroll preload lost PART$0e initialization, slot order or shared RNG.");
            }
            Compare();
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batch, afterUpdate: () =>
            {
                rom.AdvanceEnemies(1); rom.AdvanceParts(1); rom.AdvanceInteractions(1);
                Compare();
            });
            FailIf(_transitions.ScrollActive || _rooms.CurrentRoom.Id != targets[index],
                $"Palace button room $1:${targets[index]:x2} scroll did not finish.");
        }
    }

    private void ValidatePossessedNayruScrollPreloadRom()
    {
        // INTERAC$6d:$00 state 0 skips the introduction with flag$18,
        // allocating ENEMY$61:$00 during the incoming interaction pass.
        // flag$19 instead deletes the controller without allocating a boss.
        foreach (int progress in new[] { 0, 0x18, 0x19 })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            if (progress != 0) _saveData.SetGlobalFlag(progress);
            LoadValidationRoom(5, 0xd6);
            int edgeX = Enumerable.Range(8, _currentRoom.Width - 16).First(x =>
                Enumerable.Range(0, 16).All(y => !_collision.Collides(new(x, y))));
            _player.WarpTo(new(edgeX, 0));
            var seed = _random.CaptureState();
            _transitions.BeginScroll(_player, Vector2I.Up, 0xce);
            FailIf(!_transitions.ScrollActive, "Possessed Nayru fixture must enter $5:$ce through scrolling.");
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, edgeX, 6);
            rom[0xcd00] = 8; rom[0xcd02] = 0; rom[0xcc08] = 0xd0;
            rom.GeneratePlacementBuffer();
            // group5Mapd6EnemyObjectData reserves six item-drop slots. The
            // outgoing enabled-$02 allocations survive until scroll cleanup.
            for (int slot = 0; slot < 6; slot++)
            {
                int address = 0xd080 + slot * 256;
                rom[address] = 2; rom[address + 4] = 8;
            }
            // Source group5MapceObjectData, including the friendly soldier's
            // occupied slot before its state-zero story gate deletes it.
            const int controller = 0xd240;
            rom[controller] = 1; rom[controller + 1] = 0x6d;
            rom[controller + 11] = 0x4c; rom[controller + 13] = 0x78;
            rom[0xd340] = 1; rom[0xd341] = 0x40; rom[0xd342] = 0x0d; rom[0xd343] = 0x0e;
            rom[0xd34b] = 0x58; rom[0xd34d] = 0x78;
            rom.AdvanceInteractions(0);
            rom.AdvanceEnemies(0);
            void Compare()
            {
                var incoming = _entities.EntityAdapters<PalacePossessionRoomEntity>().ToArray();
                FailIf(incoming.Length != (progress == 0 ? 2 : progress == 0x18 ? 1 : 0) ||
                    incoming.Any(actor => actor.State != (actor.SubId == 0 ? rom[controller + 4] : 1)),
                    $"Possession introduction must retain its initialized state during scrolling, flag=${progress:x2}, batch={batch}.");
                var bosses = _entities.EntityAdapters<PossessedNayruRoomEntity>().ToArray();
                FailIf(bosses.Length != (progress == 0x18 ? 1 : 0),
                    $"Possessed Nayru scroll allocation differs, flag=${progress:x2}, batch={batch}.");
                foreach (var boss in bosses)
                {
                    int address = 0xd080 + boss.Slot * 256;
                    FailIf(boss.Slot != 6 || rom[address] == 0 || !boss.Node.Visible || (rom[address + 0x1a] & 0x80) == 0 ||
                        boss.Actor.State != 8 || boss.Actor.State != rom[address + 4] ||
                        boss.Actor.Speed != 0x50 || boss.Actor.Speed != rom[address + 0x10] ||
                        boss.Actor.Counter != rom[address + 6] || boss.Actor.Position != new Vector2(0x78, 0x4c),
                        $"ENEMY$61:$00 must preload its visible state$08 without advancing the fight, batch={batch}: " +
                        $"slot={boss.Slot}, port={boss.Node.Visible}/{boss.Actor.State:x2}/{boss.Actor.Speed:x2}/{boss.Actor.Counter}/{boss.Actor.Position}, " +
                        $"ROM=${rom[address]:x2}/${rom[address + 0x1a]:x2}/${rom[address + 4]:x2}/${rom[address + 0x10]:x2}/{rom[address + 6]}/" +
                        $"({rom.Word(address + 12) / 256f},{rom.Word(address + 10) / 256f}).");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || _dialogue.IsOpen ||
                    _entities.EntityAdapters<VeranProjectileRoomEntity>().Any(),
                    $"Possessed Nayru scroll must retain source RNG and freeze fight effects, flag=${progress:x2}, batch={batch}.");
            }
            Compare();
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batch, afterUpdate: () =>
            {
                rom.AdvanceEnemies(_entities.FrameCounter); rom.AdvanceInteractions(_entities.FrameCounter);
                Compare();
            });
            FailIf(_transitions.ScrollActive || _rooms.CurrentRoom.Id != 0xce,
                "Possessed Nayru scroll must complete in $5:$ce.");
            if (progress == 0x18)
            {
                rom.ClearOutgoingEnemies();
                rom[0xcd00] = 1;
                rom.AdvanceEnemies(_entities.FrameCounter + 1); rom.AdvanceInteractions(_entities.FrameCounter + 1);
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                var boss = _entities.EntityAdapters<PossessedNayruRoomEntity>().Single();
                int address = 0xd080 + boss.Slot * 256;
                FailIf(boss.Actor.State != 9 || boss.Actor.Counter != 60 || boss.Actor.State != rom[address + 4] ||
                    boss.Actor.Counter != rom[address + 6] || _entities.EntityAdapters<PalacePossessionRoomEntity>().Any(),
                    "Possessed Nayru must start state$09 on the first eligible update after scrolling.");
            }
        }
    }
}
