using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static T SomariaPrivate<T>(object owner, string field) =>
        (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private SomariaRom PrepareSomariaMotionRom(int direction, int level = 1, int ring = 0xff, bool primary = true,
        bool pushRoute = false)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0x91);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
        _inventory.GiveTreasure(TreasureId.Bracelet, level);
        typeof(InventoryState).GetMethod("SetVariable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_inventory, [TreasureVariable.BraceletLevel, level]);
        if (ring != 0xff)
        {
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Cannot equip Somaria throw ring ${ring:x2}.");
        }
        _inventory.EquipA(primary ? TreasureId.CaneOfSomaria : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.CaneOfSomaria);
        _player.WarpTo(new(120, 128));
        StepGameplayUpdates(16, Vector2.Up);
        FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
            "Somaria motion must enter through actual room $4:$91 floor geometry.");
        if (pushRoute)
        {
            Vector2 forward = OracleObjectMath.StrictCardinalVector(direction * 8);
            // Choose three open source floor tiles in the original layout.
            // No tile/collision overrides are used to make a push reachable.
            Vector2 offset = direction switch { 0 => new(0, -20), 1 => new(19, 0),
                2 => new(0, 19), _ => new(-20, 0) };
            Vector2 start = Enumerable.Range(2, _currentRoom.HeightInTiles - 4)
                .SelectMany(y => Enumerable.Range(2, _currentRoom.WidthInTiles - 4)
                    .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => !_collision.Collides(point) && Enumerable.Range(0, 3).All(distance =>
                {
                    Vector2 target = point + offset + forward * distance * 16;
                    return target.X >= 16 && target.Y >= 16 && target.X < _currentRoom.Width - 16 &&
                        target.Y < _currentRoom.Height - 16 && _currentRoom.GetMetatile(target) == 0xa0 &&
                        _currentRoom.GetTerrainInfo(target).Collision == 0;
                }));
            _player.WarpTo(start);
        }
        _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, direction,
            (int)_player.Position.X, (int)_player.Position.Y);
        rom.InitializeLinkGameplay();
        return rom;
    }

    private void CompareSomariaMotionRom(SomariaRom rom, string context)
    {
        Vector2 link = new(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f);
        FailIf(_player.PrecisePosition != link || CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
            $"{context}: Link position/facing runtime={_player.PrecisePosition}/{_player.FacingVector}, ROM={link}/{rom[0xd008]}.");
        FailIf(_playerWorld.TilePushingDirection != rom[0xcc65],
            $"{context}: late push signal runtime=${_playerWorld.TilePushingDirection:x2}, ROM=${rom[0xcc65]:x2}.");
        FailIf(_player.IsCarryingObject != ((rom[0xcc5a] & 0x80) != 0) ||
            _player.BraceletLiftCollisionsDisabled !=
                (rom[0xd200] != 0 && rom[0xd201] == 0x16 && rom[0xd204] == 2),
            $"{context}: lift/carry ownership runtime={_player.IsCarryingObject}/{_player.BraceletLiftCollisionsDisabled}, ROM grab=${rom[0xcc5a]:x2}.");
        if (rom[0xd200] != 0 && rom[0xd201] == 0x16)
        {
            BraceletState state = rom[0xd204] switch
            {
                0 => BraceletState.SeekingWall,
                1 => BraceletState.GrabbingWall,
                2 => BraceletState.LiftingEntity,
                3 => BraceletState.Idle,
                4 => BraceletState.Throwing,
                _ => throw new InvalidOperationException($"{context}: unsupported parent ITEM$16 state ${rom[0xd204]:x2}.")
            };
            FailIf(_bracelet.State != state, $"{context}: parent ITEM$16 runtime={_bracelet.State}, ROM={state}.");
        }
        FailIf(rom[0xdc00] != 0 || _bracelet.LiftedObject != null,
            $"{context}: dynamic ITEM$18 must remain the physical child; no reserved ITEM$dc may be allocated.");
        var blocks = _entities.EntityAdapters<SomariaBlockRoomEntity>().Where(entity => !entity.Finished)
            .OrderBy(_entities.DynamicItemSlotOf).ToArray();
        int[] slots = rom.Blocks;
        FailIf(blocks.Length != slots.Length, $"{context}: ITEM$18 count runtime={blocks.Length}, ROM={slots.Length}.");
        for (int index = 0; index < slots.Length; index++)
        {
            var block = blocks[index].Block;
            int slot = slots[index];
            Vector2 point = new(rom[slot + 0xd], rom[slot + 0xb]);
            FailIf(_entities.DynamicItemSlotOf(blocks[index]) != slot >> 8 ||
                block.State != rom[slot + 4] || block.Substate != rom[slot + 5] || block.Position != point ||
                block.ZHigh != unchecked((sbyte)rom[slot + 0xf]) || block.Flags != rom[slot + 0x2f] ||
                block.Health != rom[slot + 0x29] || (byte)block.DamageToApply != rom[slot + 0x25] ||
                block.Collision != rom[slot + 0x24] || block.Radius != new Vector2I(rom[slot + 0x27], rom[slot + 0x26]) ||
                block.Visible != ((rom[slot + 0x1a] & 0x80) != 0),
                $"{context}: ITEM${slot >> 8:x2} runtime={block.State}:{block.Substate}/{block.Position}/z={block.ZHigh}/flags=${block.Flags:x2}/health=${block.Health:x2}/damage={block.DamageToApply}/radius={block.Radius}; ROM={rom[slot + 4]}:{rom[slot + 5]}/{point}/z={unchecked((sbyte)rom[slot + 0xf])}/flags=${rom[slot + 0x2f]:x2}/health=${rom[slot + 0x29]:x2}/damage={unchecked((sbyte)rom[slot + 0x25])}/radius={rom[slot + 0x27]},{rom[slot + 0x26]}.");
            if (block.State == 4)
                FailIf(block.Counter != rom[slot + 6] || SomariaPrivate<Vector2>(block, "_precisePosition") !=
                    new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f),
                    $"{context}: push counter/fractional position differs.");
            if (block.State == 2 && block.Substate >= 2 && SomariaPrivate<SomariaThrowMotion?>(block, "_throw") is { } motion)
            {
                Vector2 position = SomariaPrivate<Vector2>(motion, "_position");
                int z = SomariaPrivate<int>(motion, "_z");
                FailIf(position != new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f) ||
                    (ushort)z != rom.Word(slot + 0xe) || motion.SpeedZ != unchecked((short)rom.Word(slot + 0x14)) ||
                    motion.Speed != rom[slot + 0x10] || motion.Angle != rom[slot + 9],
                    $"{context}: flight runtime={position}/z=${z & 0xffff:x4}/vz={motion.SpeedZ}/speed=${motion.Speed:x2}/angle=${motion.Angle:x2}; ROM={rom.Word(slot + 0xc) / 256f},{rom.Word(slot + 0xa) / 256f}/z=${rom.Word(slot + 0xe):x4}/vz={unchecked((short)rom.Word(slot + 0x14))}/speed=${rom[slot + 0x10]:x2}/angle=${rom[slot + 9]:x2}.");
            }
        }
        for (int y = 0; y < _currentRoom.HeightInTiles; y++)
        for (int x = 0; x < _currentRoom.WidthInTiles; x++)
        {
            int packed = y * 16 + x;
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + packed] ||
                _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + packed] ||
                _currentRoom.GetUnderlyingStorageMetatile(packed) != rom.Underlying(packed),
                $"{context}: layout/collision/shared underlying buffer differs at ${packed:x2}.");
        }
    }

    private void StepSomariaMotionRom(SomariaRom rom, int count, bool batched, int angle = 0xff,
        int held = 0, int pressed = 0, Action? afterUpdate = null)
    {
        int update = 0;
        int keys = angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, 24 => 0x20, _ => 0 };
        string[] Buttons(int mask) => new[] { "attack", "item" }.Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
        StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
            Buttons(held), Buttons(pressed), batched, () =>
            {
                rom.UpdateGameplay(update == 0 ? pressed : 0, held | keys, angle, _entities.FrameCounter);
                CompareSomariaMotionRom(rom, $"Somaria motion angle=${angle:x2}, batch={batched}, update={update++}");
                afterUpdate?.Invoke();
            });
    }

    private void EquipSomariaMotionItem(SomariaRom rom, int treasure, bool primary)
    {
        _inventory.EquipA(primary ? treasure : 0); _inventory.EquipB(primary ? 0 : treasure);
        rom[0xc689] = primary ? (byte)treasure : (byte)0;
        rom[0xc688] = primary ? (byte)0 : (byte)treasure;
    }

    private void ValidateSomariaCarryThrowRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int ring in new[] { 0xff, 0x12 })
        foreach (bool drop in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(direction, ring: ring, primary: primary);
            var audit = _sound.AttachPlayRequestAudit();
            var seed = _random.CaptureState();
            int button = primary ? 1 : 2;
            void Audit()
            {
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    !audit.Requests.SequenceEqual(rom.Sounds), "Somaria carry/throw sound or global RNG order differs.");
            }
            void Step(int count, int angle = 0xff, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, angle, held, pressed, Audit);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                EquipSomariaMotionItem(rom, TreasureId.CaneOfSomaria, primary);
                Step(1, held: button, pressed: button); Step(24);
                FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 3, "Cane must create a solid block before pickup.");
                EquipSomariaMotionItem(rom, TreasureId.Bracelet, primary);
                Step(12, direction * 8);
                FailIf(_currentRoom.IsSolid(_player.Position),
                    $"Somaria pickup approach entered solid geometry: direction={direction}, ring=${ring:x2}, drop={drop}, repeat={repeat}, Link={_player.Position}, block={_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block.Position}.");
                Step(1, held: button, pressed: button);
                FailIf(rom[0xcc5a] != 0xc2 || rom[rom.Blocks.Single() + 5] != 1,
                    "Source ITEM$18 must begin its native weight-$00 lift on the button update.");
                Step(12, held: button);
                FailIf(rom[0xcc5a] != 0xc2, "Somaria lift finished before the source 7/4/2 boundary.");
                Step(1, held: button);
                FailIf(rom[0xcc5a] != 0x83, "Somaria lift did not release its collision/movement mask on update13.");
                Step(3, (direction * 8 + 8) & 0x1f); // Carry movement and new facing.
                Step(1);
                int releaseDirection = rom[0xd008];
                Step(1, drop ? 0xff : releaseDirection * 8, button, button);
                FailIf(rom[0xcc5a] != 0 || rom[rom.Blocks.Single() + 5] != 2,
                    "Somaria must release the same dynamic child on a fresh item edge.");
                int slot = rom.Blocks.Single();
                // The collision pass's pending damage is deliberately not
                // consumed by ITEM$18 throw substates, even on landing.
                _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block.DamageToApply = -4;
                rom[slot + 0x25] = 0xfc;
                Step(45);
                FailIf(rom.Blocks.Length != 0 || _player.IsCarryingObject,
                    "Somaria drop/throw failed to retire on first ground contact.");
                // Return facing toward an open part of the original room;
                // actual movement above changes both runtimes independently.
                _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
                rom[0xd008] = (byte)direction;
                Step(1);
            }
        }
        GD.Print("Validated executed-US Somaria collision-reachable pickup, exact weight-$00 lift, carry motion, A/B release, four directions, ordinary/Toss drop/throw, pending damage, first-contact deletion and repeat use through split/batched gameplay updates.");
    }

    private void ValidateSomariaPushRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int level in new[] { 1, 2 })
        foreach (int direction in Enumerable.Range(0, 4))
        {
            SomariaRom rom = PrepareSomariaMotionRom(direction, level, pushRoute: true);
            var audit = _sound.AttachPlayRequestAudit();
            void Step(int count, int angle = 0xff, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, angle, held, pressed, () =>
                {
                    FailIf(_pushBlocks.RemainingPushFrames != rom[0xcc6a],
                        $"Somaria push delay runtime={_pushBlocks.RemainingPushFrames}, ROM={rom[0xcc6a]}, Link={_player.Position}, native pushing=${rom[0xcc65]:x2}, walls=${rom[0xd033]:x2}, flags=${rom[0xd034]:x2}, held=${rom[0xcc29]:x2}.");
                    FailIf(!audit.Requests.SequenceEqual(rom.Sounds), "Somaria push sound order differs.");
                });
            Step(1, held: 1, pressed: 1); Step(24);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int update = 0; update < 60 && rom[0xcc6a] == 20; update++) Step(1, direction * 8);
                FailIf(rom[0xcc6a] != 19, "Somaria push must reach the block through original room collision geometry.");
                Step(1);
                FailIf(rom[0xcc6a] != 20, "Releasing a push must reset its full delay.");
                Step(1, direction * 8);
                FailIf(rom[0xcc6a] != 20, "Restarting input must publish pushing before the next tile interaction consumes it.");
                Step(19, direction * 8);
                FailIf(rom[0xcc6a] != 1 || rom[rom.Blocks.Single() + 4] != 3,
                    "Somaria must stay solid through push delay update19.");
                Step(1, direction * 8);
                FailIf(rom[rom.Blocks.Single() + 4] != 4 || rom[rom.Blocks.Single() + 5] != 0,
                    $"Push delay update20 must signal state4 before the next movement update: dir={direction}, level={level}, repeat={repeat}, block={rom[rom.Blocks.Single() + 0xd]},{rom[rom.Blocks.Single() + 0xb]}, state={rom[rom.Blocks.Single() + 4]}:{rom[rom.Blocks.Single() + 5]}, flags=${rom[rom.Blocks.Single() + 0x2f]:x2}, Link={_player.Position}.");
                int moves = level == 2 ? 21 : 32;
                Step(1);
                FailIf(rom[rom.Blocks.Single() + 6] != moves - 1, "First push movement must decrement its 32/21 counter.");
                Step(moves - 2);
                FailIf(rom[rom.Blocks.Single() + 6] != 1, "Somaria push finished before the final counter update.");
                Step(1);
                FailIf(rom[rom.Blocks.Single() + 4] != 3, "Somaria push did not align/place on its final update.");
            }
        }
        GD.Print("Validated executed-US Somaria reachable push, cancelled countdown, 20-update input gate, 32/21 normal/Power Glove movement, fractional coordinates, tile buffer restoration and repeated pushes through split/batched gameplay updates.");
    }

    private void ValidateSomariaCarryCancellationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int liftUpdates in new[] { 0, 6, 13 })
        foreach (bool damage in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(0);
            void Step(int count, int angle = 0xff, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, angle, held, pressed);
            Step(1, held: 1, pressed: 1); Step(24);
            EquipSomariaMotionItem(rom, TreasureId.Bracelet, true);
            Step(12, 0); Step(1, held: 1, pressed: 1); Step(liftUpdates);
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            int slot = rom.Blocks.Single();
            if (damage) { block.DamageToApply = -10; rom[slot + 0x25] = 0xf6; }
            _dialogue.ShowMessage("Somaria pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3);
            FailIf(!block.IsHeld || damage && block.DamageToApply != -10,
                "Dialogue must preserve the held block, lift phase and pending damage.");
            _dialogue.Close(); rom[0xcba0] = 0;
            if (!damage) { _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); }
            Step(2);
            FailIf(rom.Blocks.Length != 0 || _player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "Held Somaria cancellation/lethal damage failed to clear the child and parent masks.");
            Step(12);
            EquipSomariaMotionItem(rom, TreasureId.CaneOfSomaria, true);
            Step(1, held: 1, pressed: 1); Step(24);
            EquipSomariaMotionItem(rom, TreasureId.Bracelet, true);
            Step(12, 0); Step(1, held: 1, pressed: 1); Step(13);
            FailIf(rom[0xcc5a] != 0x83, "Cane/Bracelet failed to repeat a reachable pickup after held deletion.");
        }
        GD.Print("Validated executed-US Somaria dialogue freeze, pending lethal damage, physical clearing at early/mid/completed lift, subsequent mask release and collision-reachable repeat pickup through split/batched gameplay updates.");
    }
}
