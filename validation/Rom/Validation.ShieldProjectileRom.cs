using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShieldProjectileRom()
    {
        int hostCase1 = 0;
        foreach (bool arrow in new[] { false, true })
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int interruption in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shield, level);
            bool primary = (direction & 1) == 0;
            _inventory.EquipA(primary ? TreasureId.Shield : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shield);
            // Bounded flight fixture: a walkable source floor with no terrain
            // reaction competing with the native post-object shield contact.
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(new(80, 64));
            _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, direction, 80, 64)
                { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2;
            StepGameplayUpdates(1, Vector2.Zero, MenuRomActions(button), MenuRomActions(button), batched,
                () => rom.UpdateGameplay(button, button, 0xff, _entities.FrameCounter));
            int angle = (direction * 8) ^ 0x10;
            Vector2 start = _player.Position + OracleObjectMath.StrictCardinalVector(direction * 8) * 32;
            if (arrow) start -= EnemyBehaviorTables.Shared.EnemyArrowSpawnOffsets[angle / 8].Vector;
            Node2D projectile = arrow
                ? _entities.Spawn<EnemyArrowProjectile>(new EnemyArrowSpawn(start, angle))
                : _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start, angle));
            const int part = 0xd0c0;
            rom[part] = 1; rom[part + 1] = arrow ? (byte)0x1a : (byte)0x18;
            rom[part + 9] = (byte)angle;
            rom[part + 0x0b] = (byte)start.Y; rom[part + 0x0d] = (byte)start.X;
            int update = 0;
            bool collided = false;
            bool released = false;
            void Step(int count = 1)
            {
                int held = released ? 0 : button;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), [], batched, () =>
                {
                    rom.UpdateGameplay(0, held, 0xff, _entities.FrameCounter);
                    bool nativeFinished = rom[part] == 0;
                    var snapshot = projectile switch
                    {
                        EnemyArrowProjectile p => (p.State, p.Angle, p.Counter, p.ZFixed, p.Finished),
                        OctorokRockProjectile p => (p.State, p.Angle, p.Counter, p.ZFixed, p.Finished),
                        _ => throw new System.InvalidOperationException()
                    };
                    string context = $"Shield projectile ${rom[part + 1]:x2} L{level} dir={direction} interrupt={interruption} update={++update}";
                    FailIf(snapshot.Finished != nativeFinished, context + ": deletion boundary differs.");
                    if (!nativeFinished)
                    {
                        bool bouncing = rom[part + 4] == (arrow ? 2 : 3);
                        Vector2 nativePosition = new(rom.Word(part + 0x0c) / 256.0f, rom.Word(part + 0x0a) / 256.0f);
                        FailIf((snapshot.State == HostileProjectileState.Bouncing) != bouncing ||
                            snapshot.Angle != rom[part + 9] || snapshot.Counter != rom[part + 6] ||
                            (snapshot.ZFixed & 0xffff) != rom.Word(part + 0x0e) || projectile.Position != nativePosition,
                            context + $": runtime={snapshot}/{projectile.Position}, ROM state=${rom[part + 4]:x2} angle=${rom[part + 9]:x2} counter=${rom[part + 6]:x2} Z=${rom.Word(part + 0x0e):x4} XY={nativePosition}.");
                    }
                    FailIf(_player.HealthQuarters != rom[0xc6aa] ||
                        !sounds.Requests.Where(id => id == SoundId.SndClink2).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndClink2)),
                        context + ": shield health or clink publication differs.");
                    collided |= (rom[part + 0x2a] & 0x80) != 0;
                });
            }
            while (!collided && update < 40) Step();
            FailIf(!collided || rom[part + 4] != 1 || rom[part + 0x2b] != 0xe4,
                "Native shield contact must publish var2a/invincibility without dispatching the bounce in the same update.");
            if (interruption == 1)
            {
                _dialogue.ShowMessage("Pending shield contact.", _player.Position.Y);
                rom[0xcba0] = 1;
                Step(5);
                _dialogue.Close(); rom[0xcba0] = 0;
            }
            if (interruption == 2) released = true;
            Step(); // Consume contact even if Link has now released Shield.
            Step(31);
            FailIf(rom[part] == 0 || rom[part + 6] != 1, "Projectile disappeared before the final bounce counter update.");
            Step();
            FailIf(rom[part] != 0, "Projectile survived bounce update $20.");
            _dialogue.Close();
            // Repeat against the same Link, parent and room after deletion;
            // reuse the native slot without retaining its collision signal.
            released = false;
            StepGameplayUpdates(1, Vector2.Zero, MenuRomActions(button), MenuRomActions(button), batched,
                () => rom.UpdateGameplay(button, button, 0xff, _entities.FrameCounter));
            projectile = arrow
                ? _entities.Spawn<EnemyArrowProjectile>(new EnemyArrowSpawn(start, angle))
                : _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start, angle));
            rom[part] = 1; rom[part + 1] = arrow ? (byte)0x1a : (byte)0x18;
            rom[part + 9] = (byte)angle;
            rom[part + 0x0b] = (byte)start.Y; rom[part + 0x0d] = (byte)start.X;
            collided = false;
            int repeatStart = update;
            while (!collided && update - repeatStart < 40) Step();
            FailIf(!collided, "Repeated shield contact failed after projectile slot reuse.");
            Step(33);
        }
        GD.Print("Validated clean-US Shield contacts with Octorok rocks and Moblin arrows at every level/direction: ordered flight/contact/bounce, fixed position/Z, clink/health, pending dialogue and release, exact deletion in split/batched application updates.");
    }
}
