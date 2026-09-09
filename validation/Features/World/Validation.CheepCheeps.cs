using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCheepCheeps()
    {
        var database = new EnemyDatabase();
        int placements = 0;
        foreach (int roomId in new[] { 0x02, 0x03, 0x09, 0x0a, 0x47, 0x48, 0x49 })
        {
            LoadValidationRoom(7, roomId);
            var rows = database.GetRoomObjects(5, roomId).ToArray();
            var fish = _entities.Entities<CheepCheepCharacter>();
            FailIf(rows.Length != fish.Count || _entities.RoomEnemyCount != rows.Length,
                $"Room $7:${roomId:x2} lost its counted opcode-$09 fish: " +
                $"rows={rows.Length}, fish={fish.Count}, count={_entities.RoomEnemyCount}.");
            for (int index = 0; index < rows.Length; index++)
            {
                RoomObjectRecord row = rows[index];
                FailIf(row.Kind != RoomObjectKind.ParameterEnemy || row.Id != 0x2c ||
                    row.Var03 == 0 || fish[index].Record.SubId != row.SubId ||
                    fish[index].Position != new Vector2(row.X, row.Y),
                    $"{row.Source} lost its order, subid, position, or var03.");
                EnemyCombatSourceDescriptor combat = database.EnemyHandlers.ResolveHandler(row)
                    .CombatSource(row, 0);
                FailIf(!combat.CountsAsEnemy || combat.KillableEnemyIndex != 0 ||
                    combat.CollisionMode != 0x94 || !combat.CollisionInitiallyEnabled,
                    $"{row.Source} lost its counted, nonpersistent combat source.");
                combat.ValidateSwordResponse(EnemySwordResponse.Knockback);
                placements++;
            }
        }
        FailIf(placements != 12, "Expected twelve clean-US Cheep Cheep placements.");
        OracleRoomData room = _world.LoadRoom(7, 0x0a);
        foreach (int subid in new[] { 0, 1 })
        foreach (int distance in new[] { 0x20, 0x60, 0, 0x80, 0x81, 0xff })
        {
            var fish = new CheepCheepCharacter();
            var record = database.ImportedEnemy(0x2c, subid);
            FailIf(record is not { Health: 2, DamageQuarters: 2, RadiusX: 6, RadiusY: 6,
                    TileBase: 6, Palette: 3, Animations.Length: 2 } ||
                !record.SourceGrayscaleInverted || record.Sprites.Single() != "spr_fireball_cheepcheep",
                $"Cheep Cheep $2c:${subid:x2} lost source graphics/combat attributes.");
            // Exercise coordinate wrap as well as zero/overflow in var03*2.
            Vector2 origin = new(4, 250);
            fish.Initialize(record, room, origin, distance);
            int calls = _random.Calls;
            FailIf(fish.Visible || fish.State != 0, "Cheep Cheep ran state 0 at construction.");
            fish.UpdateFrame();
            FailIf(!fish.Visible || fish.State != 8 || fish.Position != origin,
                "Cheep Cheep state 0 did not stop at visible state $08.");
            fish.UpdateFrame();
            int angle = subid == 0 ? 0x18 : 0x10;
            int counter = (distance * 2) & 0xff;
            int frames = counter == 0 ? 256 : counter;
            FailIf(fish.State != 9 || fish.Angle != angle || fish.Counter != counter ||
                fish.AnimationIndex != 0 || fish.Position != origin,
                "Cheep Cheep state $08 changed animation/moved or lost byte doubling.");
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Vector2 start = fish.Position;
                for (int update = 1; update <= frames; update++)
                {
                    fish.UpdateFrame();
                    float x = start.X + (subid == 0 ? (cycle == 0 ? -1 : 1) * update / 2f : 0);
                    float y = start.Y + (subid == 1 ? (cycle == 0 ? 1 : -1) * update / 2f : 0);
                    Vector2 expected = new(Mathf.FloorToInt(x) & 0xff, Mathf.FloorToInt(y) & 0xff);
                    FailIf(fish.Position != expected || fish.State != (update == frames ? 10 : 9) ||
                        fish.Counter != (update == frames ? 60 : (counter - update) & 0xff),
                        $"Cheep Cheep $2c:${subid:x2}, var03=${distance:x2}, cycle {cycle}, " +
                        $"update {update}: position={fish.Position}/{expected}, state=${fish.State:x2}, counter={fish.Counter}.");
                }
                Vector2 endpoint = fish.Position;
                for (int update = 1; update <= 59; update++)
                {
                    fish.UpdateFrame();
                    FailIf(fish.State != 10 || fish.Counter != 60 - update || fish.Position != endpoint,
                        "Cheep Cheep reversed before rest update 60.");
                }
                fish.UpdateFrame();
                angle ^= 0x10;
                FailIf(fish.State != 9 || fish.Counter != counter || fish.Position != endpoint ||
                    fish.Angle != angle || fish.AnimationIndex != ((cycle + 1) & 1) || fish.AnimationFrame != 0,
                    "Cheep Cheep rest update 60 failed to reverse angle/animation without moving.");
            }
            FailIf(fish.Position != origin || _random.Calls != calls,
                "Cheep Cheep patrol drifted or consumed shared RNG.");
            fish.Free();
        }

        LoadValidationRoom(7, 0x09);
        _entities.BeginScreenTransition(7, room, Vector2.Right * 160);
        CheepCheepCharacter incoming = _entities.Entities<CheepCheepCharacter>().Single();
        int randomCalls = _random.Calls;
        for (int update = 0; update < 8; update++) _entities.Update(1.0 / 60, _player);
        FailIf(incoming.State != 8 || !incoming.Visible || incoming.Counter != 0 ||
            incoming.Position != new Vector2(0xb8, 0x78) || incoming.AnimationIndex != 0 ||
            _random.Calls != randomCalls,
            "Room $7:$0a Cheep Cheep failed visible state-$08 preload or advanced during scrolling.");
        _entities.FinishScreenTransition();

        LoadValidationRoom(7, 0x0a);
        CheepCheepCharacter target = _entities.Entities<CheepCheepCharacter>().Single();
        FailIf(database.GetRoomObjects(5, 0x0a).Single().Var03 != 0x20,
            "Room $7:$0a must retain var03=$20.");
        _player.WarpTo(new Vector2(24, 24), recordSafe: false);
        _entities.Update(2.0 / 60, _player);
        var killedBefore = _entities.CaptureDebugState().RecentEnemyDefeats.KilledEnemies;
        FailIf(!_entities.ApplySwordHit(target.CollisionBounds.Grow(1),
                target.Position + Vector2.Left * 8, damage: 2,
                knockbackStrength: EnemyKnockbackStrength.Normal) ||
            target.Health != 0 || !target.PendingKnockbackDeath || target.CollisionEnabled,
            "Cheep Cheep sword collision lost lethal recoil or collision suppression.");
        int patrolCounter = target.Counter;
        _entities.Update(1.0 / 60, _player);
        FailIf(target.Counter != patrolCounter, "Cheep Cheep advanced its patrol during recoil.");
        for (int update = 0; update < 90; update++) _entities.Update(1.0 / 60, _player);
        FailIf(_entities.Entities<CheepCheepCharacter>().Count != 0 || _entities.RoomEnemyCount != 0 ||
            !killedBefore.SequenceEqual(_entities.CaptureDebugState().RecentEnemyDefeats.KilledEnemies),
            "Cheep Cheep death did not release its room count or changed recent-defeat bits.");
        LoadValidationRoom(7, 0x09);
        LoadValidationRoom(7, 0x0a);
        FailIf(_entities.Entities<CheepCheepCharacter>().Count != 1 || _entities.RoomEnemyCount != 1,
            "Opcode-$09 Cheep Cheep did not respawn on re-entry after defeat.");
        GD.Print("Validated all 12 Cheep Cheep placements, byte/fixed-point patrols, exact rests, combat, scrolling, and re-entry.");
    }
}
