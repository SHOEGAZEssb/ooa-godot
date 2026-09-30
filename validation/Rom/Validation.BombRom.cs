using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private BombRom PrepareBombGameplayRom(int direction = 0, int ring = 0xff, bool primary = true)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0x91);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.Bombs, 0x10);
        _inventory.EquipA(primary ? TreasureId.Bombs : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.Bombs);
        if (ring != 0xff)
        {
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip bomb ROM ring ${ring:x2}.");
        }
        _player.WarpTo(new(120, 128));
        StepGameplayUpdates(16, Vector2.Up);
        FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
            "Bomb ROM fixture must approach through actual $4:$91 entrance geometry.");
        _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
        OracleRandomState seed = _random.CaptureState();
        var rom = new BombRom(direction, ring, _inventory.Bombs, seed.Rng1 | seed.Rng2 << 8);
        rom[0xd00b] = 112;
        rom[0xd00d] = 120;
        rom[0xd004] = 1;
        rom[0xd029] = 1; // Link's fractional damage accumulator, initialized by state00.
        rom[0xc6aa] = (byte)_inventory.HealthQuarters;
        rom[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
        rom[0xcc2d] = 4;
        rom[0xcc30] = 0x91;
        rom[0xcc2e] = 1;
        rom[0xcc86] = 176;
        rom[0xcc87] = 240;
        rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
        rom[0xcc34] = (byte)_currentRoom.TilesetFlags;
        CopyBombRoomToRom(rom);
        return rom;
    }

    private void CopyBombRoomToRom(BombRom rom)
    {
        for (int y = 0; y < _currentRoom.HeightInTiles; y++)
        for (int x = 0; x < _currentRoom.WidthInTiles; x++)
        {
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            rom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(point);
            rom[0xce00 + y * 16 + x] = (byte)_currentRoom.GetTerrainInfo(point).Collision;
        }
    }

    private void CompareBombRom(BombRom rom, string context, bool parent = true)
    {
        if (parent)
        {
            BombParentState expected = rom[0xd200] == 0 ? BombParentState.Idle : rom[0xd204] switch
            {
                2 => BombParentState.Lifting,
                3 => BombParentState.Holding,
                4 => BombParentState.Throwing,
                _ => throw new InvalidOperationException($"{context}: unexpected bomb parent ${rom[0xd204]:x2}.")
            };
            FailIf(_bomb.State != expected,
                $"{context}: bomb parent runtime={_bomb.State}, ROM={expected}, grab=${rom[0xcc5a]:x2}.");
            FailIf(_player.IsCarryingObject != (rom[0xcc5a] == 0x83),
                $"{context}: Link's bomb carry ownership differs, ROM grab=${rom[0xcc5a]:x2}.");
        }
        FailIf(_inventory.Bombs != rom[0xc6b0],
            $"{context}: bomb ammo runtime=${_inventory.Bombs:x2}, ROM=${rom[0xc6b0]:x2}.");
        var actual = _entities.Entities<BombEffect>().Where(b => !b.Finished).ToArray();
        int[] slots = rom.BombSlots;
        FailIf(actual.Length != slots.Length,
            $"{context}: live ITEM $03 count runtime={actual.Length}, ROM={slots.Length}.");
        for (int i = 0; i < slots.Length; i++)
        {
            BombEffect bomb = actual[i];
            int slot = slots[i];
            BombState expected = rom[slot + 4] switch
            {
                0 or 2 => rom[slot + 5] < 2 ? BombState.Held : BombState.Thrown,
                1 => BombState.Grounded,
                0xff => BombState.Exploding,
                _ => throw new InvalidOperationException($"{context}: unexpected bomb child state ${rom[slot + 4]:x2}.")
            };
            Vector2 position = new(rom.Word(slot + 0xc) / 256.0f, rom.Word(slot + 0xa) / 256.0f);
            int z = unchecked((short)rom.Word(slot + 0xe));
            int speedZ = unchecked((short)rom.Word(slot + 0x14));
            FailIf(bomb.State != expected || bomb.PrecisePosition != position || bomb.ZFixed != z,
                $"{context}: ITEM ${slot >> 8:x2}: runtime={bomb.State}, pos={bomb.PrecisePosition}, z=${bomb.ZFixed & 0xffff:x4}; ROM={expected}, pos={position}, z=${z & 0xffff:x4}.");
            FailIf(bomb.AnimationCounter != rom[slot + 0x20],
                $"{context}: ITEM ${slot >> 8:x2} animation counter runtime={bomb.AnimationCounter}, ROM={rom[slot + 0x20]}, frame={bomb.AnimationFrame}.");
            if (expected == BombState.Thrown)
                FailIf(bomb.SpeedZ != speedZ || bomb.SpeedRaw != rom[slot + 0x10],
                    $"{context}: ITEM $03 throw speed runtime=${bomb.SpeedRaw:x2}/${bomb.SpeedZ & 0xffff:x4}, ROM=${rom[slot + 0x10]:x2}/${speedZ & 0xffff:x4}.");
            if (expected == BombState.Exploding)
            {
                FailIf(bomb.Damage != -unchecked((sbyte)rom[slot + 0x28]) ||
                    bomb.ExplosionRadius != rom[slot + 0x26] ||
                    bomb.ExplosionCollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0) ||
                    bomb.BreakProbe != unchecked((sbyte)rom[slot + 6]),
                    $"{context}: ITEM $03 blast runtime damage={bomb.Damage}, radius={bomb.ExplosionRadius}, collision={bomb.ExplosionCollisionEnabled}, probe={bomb.BreakProbe}; ROM damage={-unchecked((sbyte)rom[slot + 0x28])}, radius={rom[slot + 0x26]}, collision=${rom[slot + 0x24]:x2}, probe=${rom[slot + 6]:x2}.");
            }
        }
    }

    private void ValidateBombPickupThrowRom()
    {
        int cases = 0;
        foreach (bool batched in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int ring in new[] { 0xff, 0x12, 0x30 }) // ordinary, Toss, Bombproof
        foreach (bool drop in new[] { false, true })
        {
            BombRom rom = PrepareBombGameplayRom(direction, ring);
            var audit = _sound.AttachPlayRequestAudit();
            int update = 0;
            string context = $"Bomb direction={direction}, ring=${ring:x2}, drop={drop}, batch={batched}";
            void Step(int count, bool press = false, Vector2 movement = default)
            {
                rom.MovementKeys = drop ? 0 : direction switch { 0 => 0x40, 1 => 0x10, 2 => 0x80, _ => 0x20 };
                if (!press) rom.MovementKeys = 0;
                rom.Angle = press && !drop && update > 0 ? direction * 8 : 0xff;
                StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && (update == 0 || update == 20));
                        CompareBombRom(rom, $"{context}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            Step(1, true, drop ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(direction * 8));
            // Stop before self-hit knockback; own-bomb damage has independent
            // ROM boundary probes below, rather than feeding runtime movement
            // into the native reference.
            Step(94);
            FailIf(_bomb.Active, $"{context}: throw parent did not complete.");
            FailIf(!audit.Requests.Where(id => id is SoundId.SndPickup or SoundId.SndThrow or SoundId.SndBombLand or SoundId.SndExplosion)
                    .SequenceEqual(rom.Sounds),
                $"{context}: bomb pickup/throw/landing/explosion sound order differs from ROM.");
            cases++;
        }
        GD.Print($"Validated {cases} ROM bomb creation/lift/drop/throw/bounce lifecycles through actual gameplay, four directions and individual/batched updates.");
        CompareBombHazardsRom();
        CompareBombMovementHandoffRom();
    }

    private void ValidateBombFuseExplosionRom()
    {
        // Blast remains away from Link: isolate explosion collision geometry
        // and cleanup from Link recoil, which receives separate edge probes.
        foreach (bool batched in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x0c, 0x30 })
        {
            BombRom rom = PrepareBombGameplayRom(0, ring);
            int update = 0;
            void Step(int count, bool press = false)
            {
                rom.Angle = press && update == 20 ? 0 : 0xff;
                rom.MovementKeys = press && update == 20 ? 0x40 : 0;
                StepGameplayUpdates(count, press && update == 20 ? Vector2.Up : Vector2.Zero,
                    press ? ["attack"] : [], press ? ["attack"] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && (update == 0 || update == 20));
                        CompareBombRom(rom, $"Bomb fuse ring=${ring:x2}, batch={batched}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            Step(1, true);
            Step(140);
            FailIf(rom.BombSlots.Length != 0 || _entities.Entities<BombEffect>().Count != 0,
                "Bomb explosion must retire its dynamic slot before another bomb can be created.");
            update = 0;
            Step(1, true);
            FailIf(_inventory.Bombs != 0x08, "Bomb repeat must consume exactly one further packed-BCD bomb.");
        }
        GD.Print("Validated ROM bomb fuse, explosion damage/radius/collision/probe boundaries, cleanup and repeat through individual/batched gameplay updates.");
        CompareBombSelfDamageRom();
        CompareBombTileProbesRom();
    }

    private void ValidateBombGameplayRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x19, 0x3b, 0x30, 0x0c }) // ordinary, Bomber's, Peace, Bombproof, Blast
        {
            BombRom rom = PrepareBombGameplayRom(0, ring, primary);
            string button = primary ? "attack" : "item";
            int update = 0;
            void Step(int count, bool press = false)
            {
                StepGameplayUpdates(count, Vector2.Zero, press ? [button] : [], press ? [button] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && update == 0, primary);
                        CompareBombRom(rom, $"Bomb gameplay ring=${ring:x2}, A={primary}, batch={batched}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            _dialogue.ShowMessage("Bomb pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            Step(7);
            _dialogue.Close();
            rom[0xcba0] = 0;
            Step(140);
            FailIf(_inventory.HealthQuarters != rom[0xc6aa],
                $"Held bomb explosion ring=${ring:x2}: gameplay self damage differs from ROM.");
            // A real room replacement clears Link's ownership and all ITEMs.
            LoadValidationRoom(4, 0x91);
            rom.Call(0, 0x2c43); // dropLinkHeldItem
            rom.Call(6, 0x4878); // clearAllParentItems_body
            rom.Call(0, 0x35e3); // clearItems
            CompareBombRom(rom, "Bomb room replacement");
            _player.WarpTo(new(120, 128));
            StepGameplayUpdates(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Bomb repeat must approach the entrance floor again.");
            update = 0;
            Step(1, true);
            FailIf(_inventory.Bombs != 0x08, "Bomb after room cancellation must be reusable without refunding spent ammo.");
        }
        GD.Print("Validated ROM bomb A/B ammo, holding, Peace Ring fuse reset, dialogue pause/resume, room cancellation and repeat through actual gameplay.");
        CompareBombAllocationRom();
    }

    private void CompareBombAllocationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x19 })
        {
            BombRom rom = PrepareBombGameplayRom(1, ring, primary);
            string button = primary ? "attack" : "item";
            int update = 0;
            void Step(int count, bool press = false)
            {
                int first = update;
                StepGameplayUpdates(count, Vector2.Zero, press ? [button] : [], press ? [button] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && update == first, primary);
                        CompareBombRom(rom, $"Bomb allocation ring=${ring:x2}, A={primary}, batch={batched}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            Step(1, true); // Drop, retaining height until its gravity pass.
            Step(9);
            Step(1, true); // Right-facing drop remains below the grab prism.
            int expectedAmmo = ring == 0x19 ? 0x08 : 0x09;
            FailIf(_inventory.Bombs != expectedAmmo || rom.BombSlots.Length != (ring == 0x19 ? 2 : 1),
                "An airborne bomb outside the grab prism must use the one/two-bomb allocation cap instead of being picked up.");
            if (ring == 0x19)
            {
                Step(19);
                Step(1, true);
                Step(10);
                // Both drops are now reachable at ground height. Picking one
                // up must preserve slot order and spend no additional ammo.
            }
            else Step(30);
            int[] before = rom.BombSlots;
            int bombs = _inventory.Bombs;
            Step(1, true);
            FailIf(_bomb.State != BombParentState.Lifting || _inventory.Bombs != bombs ||
                !before.SequenceEqual(rom.BombSlots),
                "A touching grounded ITEM $03 must be re-picked in native slot order without consuming ammo or another slot.");
            Step(13);
            FailIf(_bomb.State != BombParentState.Holding, "Re-picked bomb must use the full 7/4/2 lift animation.");
            if (ring == 0x19)
            {
                Step(1, true);
                Step(9);
                // Put Link out of both grab prisms through actual floor
                // movement, then test the occupied two-bomb limit.
                int walk = 0;
                StepGameplayUpdates(12, Vector2.Down, batched: batched, afterUpdate: () =>
                {
                    rom[0xd00b] = (byte)(112 + ++walk);
                    rom[0xd008] = 2;
                    rom.Update(primary: primary);
                    CompareBombRom(rom, $"Bomb cap approach update={update++}");
                    FailIf(_player.Position != new Vector2(120, 112 + walk),
                        "The third-bomb cap probe must walk away on actual floor geometry.");
                });
                Step(1, true);
                FailIf(_bomb.Active || _inventory.Bombs != bombs || rom.BombSlots.Length != 2,
                    "Bomber's Ring must reject a third live bomb without spending ammo.");
            }
        }
        GD.Print("Validated ROM bomb airborne grab rejection, one/two-object allocation caps, slot-preserving grounded re-pickup and the distinct 7/4/2 pickup lift through A/B and individual/batched gameplay updates.");
    }

    private void CompareBombMovementHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            BombRom rom = PrepareBombGameplayRom();
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            rom.Update(true, true);
            StepGameplayUpdates(19, Vector2.Zero, batched: batched, afterUpdate: () => rom.Update());
            rom.Angle = 0;
            rom.MovementKeys = 0x40;
            int update = 0;
            StepGameplayUpdates(14, Vector2.Up, ["attack"], ["attack"], batched, afterUpdate: () =>
            {
                rom.Update(true, update == 0, moveLink: true);
                CompareBombRom(rom, $"Bomb movement handoff batch={batched}, update={update++}");
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected,
                    $"Bomb throw completion must release Link movement in the same update: runtime={_player.PrecisePosition}, ROM={expected}, parent=${rom[0xd204]:x2}.");
            });
        }
        GD.Print("Validated executed-ROM bomb throw-to-Link movement handoff before/during/completion/after completion through individual/batched gameplay updates.");
    }
}
