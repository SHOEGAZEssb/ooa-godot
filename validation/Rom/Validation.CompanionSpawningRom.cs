using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionPresetPrecedenceRom()
    {
        foreach (int subid in new[] { 0, 1, 2, 3, 4, 5 })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool remembered in new[] { true, false })
        {
            if (!remembered && subid < 4) continue; // Ordinary presets have their own scenario.
            ReinitializeGameplayForValidation();
            _inventory.AssignAnimalCompanion(id);
            _saveData.WriteWramByte(WramAddress.wCompanionStates, 0x20);
            _saveData.WriteWramByte(WramAddress.wDimitriState, 0x20);
            _saveData.WriteWramByte(WramAddress.wMooshState, 0x20);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 6);
            foreach (int flag in new[] { 0x15, 0x22, 0x24, 0x42 }) _saveData.SetGlobalFlag(flag);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetRoomFlag(1, 0x79, 0x40);
            if (subid == 1) _inventory.GiveTreasure(TreasureId.ChevalRope, 0);
            else _inventory.LoseTreasure(TreasureId.ChevalRope);
            int room = subid switch { 0 => 0x6c, 1 => 0x6b, 2 => 0x6a, 3 => 0xaa, 4 => 0x81, _ => 0x63 };
            CompanionRuntimeState.Remember(_runtimeState, remembered ? id : 0, 0, room, new(72, 64));
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtimeState, new(24, 24));
            var native = new CompanionSpawnRom(_world.LoadRoom(0, room), 0, new(8, 8));
            for (int address = 0xc600; address < 0xcb00; address++) native.Memory[address] = _saveData.ReadWramByte(address);
            for (int address = 0xcc24; address <= 0xcc28; address++) native.Memory[address] = _runtimeState.ReadWramByte(address);
            native.Memory[0xc638] = native.Memory[0xc639] = 24;
            native.Remembered(); native.Spawn(subid);
            LoadValidationRoom(0, room);
            var actors = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
            FailIf(actors.Length != 1 || actors[0].Node.Position != new Vector2(native.Memory[0xd10d], native.Memory[0xd10b]),
                $"Preset ${subid:x2} did not preserve native allocation precedence for companion ${id:x2}, remembered={remembered}.");
            for (int address = 0xcc24; address <= 0xcc28; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native.Memory[address], $"Preset ${subid:x2} precedence changed remembered byte ${address:x4}.");
            FailIf(CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState) != new Vector2(native.Memory[0xc639], native.Memory[0xc638]),
                $"Preset ${subid:x2} precedence changed the native fallback point.");
        }
    }

    private void ValidateCompanionSpawnInitializationRom()
    {
        int hostCase3 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool occupied in new[] { false, true })
        foreach (bool frozen in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom();
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x40);
            LoadValidationRoom(0, 0x45);
            var boy = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3f);
            Vector2 position = occupied ? boy.Position : new(104, 88);
            _currentRoom.SetPositionTileAndCollision(position, 0, 0, 0);
            _currentRoom.SetPositionTileAndCollision(position + new Vector2(0, 5), 0, 0, 0);
            CompanionRuntimeState.Remember(_runtimeState, id, 0, 0x45, position);
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtimeState, new(40, 40));
            _entities.LoadRoom(0, _currentRoom);
            _player.WarpTo(new(8, 8));
            var rom = new CompanionRom(id, position, 2, _currentRoom);
            rom.InitializeRemembered(_player.Position, new(40, 40), occupied);
            var palette = _entities.PaletteFadeActiveSource;
            try
            {
                _entities.PaletteFadeActiveSource = () => frozen;
                rom[0xc4ab] = frozen ? (byte)1 : (byte)0;
                int update = 0;
                StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    rom.Update(0xff); update++;
                    var actors = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
                    FailIf(actors.Length != rom[0xd100], $"Companion ${id:x2} initialization update {update}, occupied={occupied}, frozen={frozen}: slot differs.");
                    if (actors.Length != 0)
                    {
                        FailIf(actors[0].Node.Visible != (rom[0xd104] != 0), "Companion became visible before native state-$00 finished.");
                        if (rom[0xd104] != 0)
                        {
                            var animation = CompanionField<EnemyAnimationPlayer>(actors[0], "_animation");
                            FailIf(animation.AnimationIndex != rom[0xd130] || animation.CurrentParameter != rom[0xd121], "Initialized companion animation differs.");
                        }
                    }
                    FailIf(CompanionRuntimeState.ReadRemembered(_runtimeState).Id != id,
                        "Rejected companion initialization cleared remembered state.");
                });
            }
            finally { _entities.PaletteFadeActiveSource = palette; }
        }
    }

    private void ValidateCompanionSpawnScrollRom()
    {
        int hostCase2 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
            Vector2 point = new(72, 64);
            var destination = _world.LoadRoom(0, 0x2b);
            destination.SetPositionTileAndCollision(point, 0, 0, 0);
            destination.SetPositionTileAndCollision(point + new Vector2(0, 5), 0, 0, 0);
            CompanionRuntimeState.Remember(_runtimeState, id, 0, 0x2b, point);
            _player.WarpTo(new(152, 64));
            _transitions.BeginScroll(_player, Vector2I.Right, 0x2b);
            var rom = new CompanionRom(id, point, 2, _currentRoom);
            rom.InitializeRemembered(new(8, 8), new(40, 40), false);
            rom[0xcd00] = 2;
            int update = 0;
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.Update(0xff); update++;
                var actors = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
                FailIf(actors.Length != 1 || actors[0].Node.Visible != (rom[0xd104] != 0),
                    $"Companion ${id:x2} scroll initialization differs at update {update}.");
                FailIf(CompanionField<Vector2>(actors[0], "_precisePosition") != rom.Position,
                    "Incoming companion moved before scrolling completed.");
            });
            FailIf(_entities.EntityAdapters<IRoomEntity>().Count(e => e is IRoomInitializedCompanion) != 1 ||
                _entities.OutgoingEntities<Node2D>().OfType<IRoomInitializedCompanion>().Any(), "Scroll completion duplicated a remembered companion.");
        }
    }

    private void ValidateCompanionPresetsRom()
    {
        ReinitializeGameplayForValidation();
        var ricky = new RickyGlovesEventDatabase(); var dimitri = new DimitriDatabase();
        var moosh = new MooshGoodbyeEventDatabase();
        foreach (int subid in new[] { 1, 2, 3 })
        for (int state = 0; state < 256; state++)
        foreach (bool prerequisite in new[] { false, true })
        {
            // The goodbye owner explicitly rejects the pre-rescue story
            // branch; it cannot arise after legitimately obtaining the rope.
            if (subid == 1 && (state & 0x20) == 0) continue;
            _saveData.WriteWramByte(WramAddress.wCompanionStates, (byte)state);
            _saveData.WriteWramByte(WramAddress.wDimitriState, (byte)state);
            _saveData.WriteWramByte(WramAddress.wMooshState, (byte)state);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, prerequisite ? (byte)4 : (byte)0);
            _saveData.SetGlobalFlag(0x15, prerequisite);
            if (prerequisite) _inventory.GiveTreasure(TreasureId.ChevalRope, 0);
            else _inventory.LoseTreasure(TreasureId.ChevalRope);
            int room = subid == 1 ? 0x6b : subid == 2 ? 0x6a : 0xaa;
            var native = new CompanionSpawnRom(_world.LoadRoom(0, room), 0, new(8, 8));
            for (int address = 0xc600; address < 0xcb00; address++) native.Memory[address] = _saveData.ReadWramByte(address);
            native.Spawn(subid);
            bool actual = subid switch { 1 => moosh.ShouldSpawn(0, room, _saveData, _inventory),
                2 => ricky.ShouldSpawnPreset(0, room, _saveData), _ => dimitri.ShouldSpawnPreset(0, room, _saveData) };
            FailIf(actual != (native.Memory[0xd100] != 0), $"Companion preset ${subid:x2}, state=${state:x2}, prerequisite={prerequisite}: gate differs.");
        }
        foreach (int subid in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wCompanionStates, 0x20);
            _saveData.WriteWramByte(WramAddress.wDimitriState, 0x20);
            _saveData.WriteWramByte(WramAddress.wMooshState, 0x20);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 6);
            _saveData.SetGlobalFlag(0x15); _saveData.SetRoomFlag(1, 0x79, 0x40);
            if (subid == 1) _inventory.GiveTreasure(TreasureId.ChevalRope, 0);
            else _inventory.LoseTreasure(TreasureId.ChevalRope);
            CompanionRuntimeState.Remember(_runtimeState, 0x0c, 1, 0x23, new(0x45, 0x67));
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtimeState, new(24, 24));
            int room = subid == 0 ? 0x6c : subid == 1 ? 0x6b : subid == 2 ? 0x6a : 0xaa;
            var native = new CompanionSpawnRom(_world.LoadRoom(0, room), 0, new(8, 8));
            for (int address = 0xc600; address < 0xcb00; address++) native.Memory[address] = _saveData.ReadWramByte(address);
            for (int address = 0xcc24; address <= 0xcc28; address++) native.Memory[address] = _runtimeState.ReadWramByte(address);
            native.Spawn(subid);
            FailIf(native.Memory[0xd100] == 0, $"Preset ${subid:x2} fixture did not spawn natively.");
            LoadValidationRoom(0, room);
            var actors = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
            FailIf(actors.Length != 1 || actors[0].Node.Position != new Vector2(native.Memory[0xd10d], native.Memory[0xd10b]),
                $"Preset ${subid:x2} did not allocate one companion at the native preset position.");
            for (int address = 0xcc24; address <= 0xcc28; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native.Memory[address], $"Preset ${subid:x2} remembered byte ${address:x4} differs.");
            FailIf(CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState) != new Vector2(native.Memory[0xc639], native.Memory[0xc638]),
                $"Preset ${subid:x2} did not install its native last-mount point.");
        }
    }

    private void ValidateCompanionFluteGatesRom()
    {
        ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
        var data = new FluteDatabase();
        var idField = typeof(OracleRoomData).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var flagsField = typeof(OracleRoomData).GetField("<TilesetFlags>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int originalId = _currentRoom.Id; byte originalFlags = _currentRoom.TilesetFlags;
        int cases = 0;
        void Check(int room, byte flags, int icon, int occupant)
        {
            _entities.Clear();
            foreach (int id in new[] { 0x0a, 0x0b, 0x0c, 0x0d, 0x13 }) CompanionRuntimeState.Clear(_runtimeState, id);
            idField.SetValue(_currentRoom, room); flagsField.SetValue(_currentRoom, flags);
            _inventory.AssignAnimalCompanion(0x0c);
            if (occupant != 0) CompanionRuntimeState.Begin(_runtimeState, occupant, room, new(72, 64), 2);
            var native = new CompanionSpawnRom(_currentRoom, 0, _player.Position);
            native.Memory[0xc610] = 0x0c; native.Memory[0xc6b5] = (byte)icon;
            native.Memory[0xd100] = occupant == 0 ? (byte)0 : (byte)1;
            native.Memory[0xd101] = (byte)occupant;
            native.Spawn(0x80);
            string? message = null;
            new CompanionFluteSpawner(_rooms, _entities, data, (text, _) => message = text).Call(_player, icon);
            int actual = _entities.EntityAdapters<IRoomEntity>().Count(actor => actor is IPlayerRideableRoomEntity);
            bool spawned = occupant == 0 && native.Memory[0xd100] != 0;
            FailIf(actual != (spawned ? 1 : 0), $"Flute room=${room:x2}, flags=${flags:x2}, icon={icon}, slot=${occupant:x2}: allocation differs.");
            int textId = ((native.Memory[0xcba3] - 4) << 8) | native.Memory[0xcba2];
            FailIf(message != (native.Memory[0xcba0] == 0 ? null : data.Text(textId)),
                $"Flute room=${room:x2}, flags=${flags:x2}, icon={icon}, slot=${occupant:x2}: text differs.");
            cases++;
        }
        try
        {
            for (int room = 0; room < 256; room++) Check(room, 1, 1, 0);
            for (int flags = 0; flags < 256; flags++)
            for (int icon = 0; icon < 4; icon++)
            foreach (int occupant in new[] { 0, 0x0a, 0x0b, 0x0c, 0x0d, 0x13 })
                Check(0x2a, (byte)flags, icon, occupant);
        }
        finally
        {
            idField.SetValue(_currentRoom, originalId); flagsField.SetValue(_currentRoom, originalFlags);
            foreach (int id in new[] { 0x0a, 0x0b, 0x0c, 0x0d, 0x13 }) CompanionRuntimeState.Clear(_runtimeState, id);
        }
        GD.Print($"Validated {cases} native flute room, tileset, icon and occupied-slot gates including text selection.");
    }

    private void ValidateCompanionRememberedSpawnRom()
    {
        int hostCase1 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int collision in new[] { 0, 1, 0x0c, 0x0f, 0x10, 0x11 })
        foreach (int fallbackCollision in new[] { 0, 1, 15 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom();
            Vector2 position = new(72, 60), fallback = new(104, 88);
            _currentRoom.SetPositionTileAndCollision(position + new Vector2(0, 5), 0, (byte)collision, 0);
            _currentRoom.SetPositionTileAndCollision(fallback, 0, (byte)fallbackCollision, 0);
            CompanionRuntimeState.Remember(_runtimeState, id, 0, 0x2a, position);
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtimeState, fallback);
            _entities.LoadRoom(0, _currentRoom);
            var rom = new CompanionRom(id, position, 2, _currentRoom);
            rom.InitializeRemembered(_player.Position, fallback, false);
            int update = 0;
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.Update(0xff); update++;
                var actors = _entities.EntityAdapters<IRoomEntity>().Where(actor => actor is
                    RickyCompanionRoomEntity or DimitriCompanionRoomEntity or MooshCompanionRoomEntity).ToArray();
                if (update == 1) return;
                FailIf(actors.Length != rom[0xd100], $"Remembered ${id:x2} collision=${collision:x2}, fallback=${fallbackCollision:x2}: allocation differs.");
                if (actors.Length != 0)
                    FailIf(CompanionField<Vector2>(actors[0], "_precisePosition") != rom.Position,
                        $"Remembered ${id:x2} collision=${collision:x2}, fallback=${fallbackCollision:x2}: native={rom.Position}, runtime={actors[0].Node.Position}.");
                FailIf(CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState) != fallback,
                    "Restoring a remembered companion changed the last mount point.");
            });
        }
    }

    private void ValidateCompanionFluteSpawnRom()
    {
        ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
        var data = new FluteDatabase();
        int spawned = 0;
        // All twenty ordered candidates, both cells in each pair, and ties.
        var candidates = new (int Packed, int Stride)[] { (2,16), (0x62,16), (0x18,1), (0x10,1),
            (3,16), (4,16), (5,16), (6,16), (0x63,16), (0x64,16), (0x65,16), (0x66,16),
            (0x28,1), (0x38,1), (0x48,1), (0x58,1), (0x20,1), (0x30,1), (0x40,1), (0x50,1) };
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        for (int candidate = 0; candidate <= candidates.Length; candidate++)
        foreach (int blockedCell in new[] { -1, 0, 1 })
        {
            _entities.Clear(); _inventory.AssignAnimalCompanion(id);
            _player.WarpTo(new(40, 24));
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0, 15, 0);
            for (int index = candidate; index < candidates.Length; index++)
            {
                var pair = candidates[index];
                foreach (int packed in new[] { pair.Packed, pair.Packed + pair.Stride })
                    _currentRoom.SetPositionTileAndCollision(new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8), 0, 0, 0);
            }
            if (candidate < candidates.Length && blockedCell >= 0)
            {
                int packed = candidates[candidate].Packed + blockedCell * candidates[candidate].Stride;
                _currentRoom.SetPositionTileAndCollision(new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8), 0, 1, 0);
            }
            var native = new CompanionSpawnRom(_currentRoom, 0, _player.Position);
            native.Memory[0xc610] = (byte)id; native.Memory[0xc6b5] = (byte)(id - 0x0a);
            native.Spawn(0x80);
            string? message = null;
            new CompanionFluteSpawner(_rooms, _entities, data, (text, _) => message = text).Call(_player, id - 0x0a);
            var actors = _entities.EntityAdapters<IRoomEntity>().Where(actor => actor is IPlayerRideableRoomEntity).ToArray();
            FailIf(actors.Length != native.Memory[0xd100], $"Flute candidate={candidate}, blocked={blockedCell}, companion=${id:x2}: allocation differs.");
            if (actors.Length != 0)
            {
                spawned++;
                var actor = (IRoomEntity)actors.Single();
                Vector2 p = CompanionField<Vector2>(actor, "_precisePosition");
                Vector2 mount = CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState);
                FailIf((byte)(int)p.X != native.Memory[0xd10d] || (byte)(int)p.Y != native.Memory[0xd10b] ||
                    CompanionField<int>(actor, "_direction") != native.Memory[0xd108] ||
                    mount != new Vector2(native.Memory[0xc639], native.Memory[0xc638]),
                    $"Flute candidate={candidate}, blocked={blockedCell}, companion=${id:x2}: entrance or fallback point differs.");
            }
            FailIf((message is not null) != (native.Memory[0xcba0] != 0), "Flute failure message gate differs.");
        }
        FailIf(spawned < 60, $"Flute entrance fixtures exercised only {spawned} successful spawns.");
    }
}
