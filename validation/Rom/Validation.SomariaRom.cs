using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaSwingRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
            RunSomariaRom(batched, primary, direction, "repeat");
        GD.Print("Validated executed-US Cane A/B swings in four directions: parent/weapon timing, geometry, phase-in, repeated use, native slot replacement, tile restoration, sound and RNG through split/batched gameplay updates.");
    }

    private void ValidateSomariaBlockRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string scenario in new[] { "restart", "cancel", "phase-clear", "solid-clear", "damage", "full-pool" })
            RunSomariaRom(batched, true, 0, scenario);
        GD.Print("Validated executed-US Cane restart, cancellation, frozen/marked phase-in, solid clearing, signed health damage and failed full-pool replacement through split/batched gameplay updates.");
    }

    private void RunSomariaRom(bool batched, bool primary, int direction, string scenario)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0x91);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
        _inventory.EquipA(primary ? TreasureId.CaneOfSomaria : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.CaneOfSomaria);
        _player.WarpTo(new(120, 128));
        StepGameplayUpdates(16, Vector2.Up);
        FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
            "Somaria ROM fixture must approach through actual $4:$91 entrance geometry.");
        _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
        var seed = _random.CaptureState();
        var rom = new SomariaRom(_saveData, seed, _currentRoom, direction, 120, 112);
        var audit = _sound.AttachPlayRequestAudit();
        byte[] inventory = Enumerable.Range(0xc688, 0x38).Select(_saveData.ReadWramByte).ToArray();
        int update = 0;
        string Context() => $"Somaria {scenario} dir={direction}, A={primary}, batch={batched}, update={update}";
        T Private<T>(object owner, string field) => (T)owner.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        void Compare()
        {
            string context = Context();
            var cane = _entities.Somaria!;
            FailIf(cane.Active != (rom[0xd200] != 0), $"{context}: parent lifetime differs.");
            if (cane.Active)
                FailIf(cane.Parent!.Parameter != rom[0xd221] || Private<int>(cane.Parent, "_counter") != rom[0xd220],
                    $"{context}: parent parameter/counter runtime=${cane.Parent.Parameter:x2}/{Private<int>(cane.Parent, "_counter")}, ROM=${rom[0xd221]:x2}/{rom[0xd220]}.");
            FailIf((cane.Weapon != null) != (rom[0xd600] != 0), $"{context}: reserved ITEM$04 lifetime differs.");
            if (cane.Weapon is { } weapon)
            {
                Vector2 expected = new(rom[0xd60d], rom[0xd60b]);
                FailIf(weapon.State != rom[0xd604] || weapon.Position != expected ||
                    weapon.ZHigh != unchecked((sbyte)rom[0xd60f]) ||
                    weapon.Radius != new Vector2I(rom[0xd627], rom[0xd626]) || weapon.Collision != rom[0xd624],
                    $"{context}: weapon state/position/Z/radius/collision runtime={weapon.State}/{weapon.Position}/{weapon.ZHigh}/{weapon.Radius}/${weapon.Collision:x2}, ROM={rom[0xd604]}/{expected}/{unchecked((sbyte)rom[0xd60f])}/{rom[0xd627]},{rom[0xd626]}/${rom[0xd624]:x2}.");
            }
            FailIf(_player.Position != new Vector2(rom[0xd00d], rom[0xd00b]) ||
                CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                $"{context}: Link position/facing differs.");
            var blocks = _entities.EntityAdapters<SomariaBlockRoomEntity>()
                .Where(entity => !entity.Finished).OrderBy(_entities.DynamicItemSlotOf).ToArray();
            int[] slots = rom.Blocks;
            FailIf(blocks.Length != slots.Length, $"{context}: ITEM$18 count runtime={blocks.Length}, ROM={slots.Length}.");
            for (int index = 0; index < slots.Length; index++)
            {
                var block = blocks[index].Block;
                int slot = slots[index];
                FailIf(_entities.DynamicItemSlotOf(blocks[index]) != slot >> 8,
                    $"{context}: ITEM$18 native allocation order differs.");
                FailIf(block.State != rom[slot + 4] || block.Substate != rom[slot + 5] ||
                    block.Position != new Vector2(rom[slot + 0xd], rom[slot + 0xb]) ||
                    block.ZHigh != unchecked((sbyte)rom[slot + 0xf]) || block.Flags != rom[slot + 0x2f] ||
                    block.Health != rom[slot + 0x29] || block.Collision != rom[slot + 0x24] ||
                    block.Radius != new Vector2I(rom[slot + 0x27], rom[slot + 0x26]) ||
                    block.Visible != ((rom[slot + 0x1a] & 0x80) != 0),
                    $"{context}: ITEM${slot >> 8:x2} state/position/flags/health/collision/radius/visibility differs: runtime={block.State}/{block.Position}/${block.Flags:x2}/${block.Health:x2}/${block.Collision:x2}/{block.Radius}/{block.Visible}, ROM={rom[slot + 4]}/{rom[slot + 0xd]},{rom[slot + 0xb]}/${rom[slot + 0x2f]:x2}/${rom[slot + 0x29]:x2}/${rom[slot + 0x24]:x2}/{rom[slot + 0x27]},{rom[slot + 0x26]}/${rom[slot + 0x1a]:x2}.");
                var visual = Private<SomariaBlockVisual>(block, "_visual");
                var animation = Private<EnemyAnimationPlayer[]>(visual, "_animations")[visual.Pose];
                FailIf(visual.Parameter != rom[slot + 0x21] || Private<int>(animation, "_frameCounter") != rom[slot + 0x20],
                    $"{context}: ITEM$18 phase/solid animation parameter/counter differs.");
                if (block.State == 3)
                    FailIf(block.PackedPosition != rom[slot + 0x32], $"{context}: solid block tile position differs.");
            }
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
            {
                int packed = y * 16 + x;
                Vector2 point = new(x * 16 + 8, y * 16 + 8);
                FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + packed] ||
                    _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + packed] ||
                    _currentRoom.GetUnderlyingStorageMetatile(packed) != rom.Underlying(packed),
                    $"{context}: tile/collision/shared underlying buffer differs at ${packed:x2}.");
            }
            var random = _random.CaptureState();
            FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                $"{context}: ordered global RNG differs.");
            FailIf(!audit.Requests.SequenceEqual(rom.Sounds), $"{context}: sound order runtime={string.Join(',', audit.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
            FailIf(!inventory.SequenceEqual(Enumerable.Range(0xc688, 0x38).Select(_saveData.ReadWramByte)),
                $"{context}: Cane use changed inventory bytes.");
        }
        void Step(int count, bool pressed = false)
        {
            string button = primary ? "attack" : "item";
            int edge = pressed ? primary ? 1 : 2 : 0;
            StepGameplayUpdates(count, Vector2.Zero, pressed ? [button] : [], pressed ? [button] : [], batched, () =>
            {
                rom.Update(edge, pressed ? primary ? 1 : 2 : 0, _entities.FrameCounter);
                edge = 0; update++; Compare();
            });
        }
        void Clear()
        {
            _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Compare();
        }
        void RepeatAfterDeletion()
        {
            Step(1, true); Step(24);
            FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 3,
                "Cane failed to create another solid block after cancellation/deletion.");
        }
        Step(1, true);
        Step(12);
        if (scenario == "cancel")
        {
            Clear(); Step(30);
            FailIf(rom.Blocks.Length != 0, "Cancelled Somaria swing reached its creation trigger.");
            RepeatAfterDeletion();
            return;
        }
        if (scenario == "restart") { Step(1, true); Step(13); }
        else Step(1);
        FailIf(rom.Blocks.Length != 0, "Somaria created a block before native parameter$06.");
        Step(1);
        FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 1,
            "Somaria did not initialize a phasing block on the native creation update.");
        if (scenario == "phase-clear")
        {
            Clear();
            var textSource = _entities.TextActiveSource;
            try { _entities.TextActiveSource = () => true; rom[0xcba0] = 1; Step(2); }
            finally { _entities.TextActiveSource = textSource; rom[0xcba0] = 0; }
            Step(8);
            FailIf(rom[rom.Blocks.Single() + 4] != 1, "Marked block phase-in finished early.");
            Step(1);
            FailIf(rom[rom.Blocks.Single() + 4] != 3, "Marked block failed to place its hidden solid tile.");
            Step(1);
            FailIf(rom.Blocks.Length != 0, "Marked solid block did not restore its tile on its next eligible update.");
            RepeatAfterDeletion();
            return;
        }
        Step(9);
        FailIf(_entities.Somaria!.Active || rom[rom.Blocks.Single() + 4] != 3,
            "Somaria swing/phase-in missed native completion boundaries.");
        Step(2);
        if (scenario == "solid-clear") { Clear(); Step(2); RepeatAfterDeletion(); return; }
        if (scenario == "damage")
        {
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            int slot = rom.Blocks.Single();
            FailIf(rom[slot + 0x29] != 0x09, "Original ITEM$18 attributes must initialize health to $09.");
            block.DamageToApply = -9; rom[slot + 0x25] = 0xf7; Step(1);
            FailIf(rom.Blocks.Length != 1 || rom[slot + 0x29] != 0,
                "ITEM$18 health $09->$00 must survive.");
            block.DamageToApply = -1; rom[slot + 0x25] = 0xff; Step(1);
            FailIf(rom.Blocks.Length != 0, "ITEM$18 signed health underflow failed to delete/restore.");
            Step(2); RepeatAfterDeletion(); return;
        }
        if (scenario == "full-pool")
        {
            var data = new BombDatabase().Data;
            for (int i = 0; i < 4; i++)
            {
                _entities.Spawn<BombEffect>(new BombSpawn(_player, data, 0, _ => { }));
                int slot = Enumerable.Range(0xd7, 5).Select(page => page << 8).First(address => rom[address] == 0);
                rom[slot] = 1; rom[slot + 1] = 3;
                rom[slot + 0xb] = 112; rom[slot + 0xd] = 120;
            }
        }
        Step(1, true); Step(14); Step(10);
        FailIf(rom.Blocks.Length != (scenario == "full-pool" ? 0 : 1),
            "Somaria replacement allocation/failure differed from the source boundary.");
    }
}
