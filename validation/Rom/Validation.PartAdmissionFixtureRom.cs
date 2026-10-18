using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private SomariaRom PrepareEnemyPartAdmissionRom(int id, int subId, out EnemyCharacter enemy)
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(id == 0x36 ? 2 : 0, id == 0x36 ? 0xcf : 0x33);
        if (id != 0x36) _entities.Clear();
        _inventory.EquipA(0); _inventory.EquipB(0);
        _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
        Vector2 start = (from y in Enumerable.Range(3, 4) from x in Enumerable.Range(2, 6)
            let point = new Vector2(x * 16 + 8, y * 16 + 8)
            where _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None &&
                Enumerable.Range(-8, 17).All(dx => Enumerable.Range(-8, 17).All(dy =>
                    !_currentRoom.IsSolid(point + new Vector2(dx, dy)))) select point).First();
        if (id == 0x36)
        {
            // Cuccos use ordered room construction, not the standalone combat
            // factory. Retain the first actual ENEMY$36 owner in room$2:$cf.
            var cucco = _entities.Entities<CuccoCharacter>().First();
            enemy = cucco;
            foreach (var actor in _entities.EntityAdapters<IRoomEntity>().Where(actor => !ReferenceEquals(actor.Node, cucco)).ToArray())
                _entities.RetireEntity(actor);
            enemy.Position = start;
        }
        else
        {
            FailIf(!_entities.TrySpawnEnemy(id, subId, start, "PART admission regression", out string error), error);
            enemy = _entities.Entities<EnemyCharacter>().Single();
        }
        StepGameplayUpdates(1, Vector2.Zero);
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24);
        native[0xd080] = 1; native[0xd081] = (byte)id; native[0xd082] = (byte)subId;
        native[0xd08b] = (byte)start.Y; native[0xd08d] = (byte)start.X;
        native.AdvanceEnemies(_entities.FrameCounter);
        // Declare an initialized boundary. Common initialization/RNG and
        // roaming before that boundary are not certified by this fixture.
        enemy.Position = start;
        native.Word(0xd08a, (int)start.Y * 256); native.Word(0xd08c, (int)start.X * 256);
        var seed = _random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
        return native;
    }

    private void OccupyPartAdmissionSlotsRom(SomariaRom native, int free)
    {
        for (int slot = 0; slot < 16 - free; slot++)
        {
            _entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(slot % 8, new(120, 40), 0xff));
            int at = 0xd0c0 + slot * 256;
            native[at] = 1; native[at + 1] = 0x53; native[at + 3] = (byte)(slot % 8);
            native[at + 6] = 0xff; native[at + 0x30] = 40; native[at + 0x31] = 120;
        }
    }

    private void AdvancePartAdmissionRom(SomariaRom native)
    {
        // updateAllObjects publishes these before its ENEMY/PART walks.
        native[0xffb0] = (byte)_player.Position.Y; native[0xffb1] = (byte)_player.Position.X;
        native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceParts(_entities.FrameCounter);
    }
}
