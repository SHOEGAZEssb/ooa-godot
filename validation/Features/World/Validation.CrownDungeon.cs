using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonArrowMoblins()
    {
        // enemy0cSubidData: $11/$10 selects extraEnemyData[$11]
        // ($06,$06,$fc,$05), palette 1 and the shared four animations.
        var database = new EnemyDatabase();
        var definition = database.ImportedEnemy(EnemyId.ArrowMoblin, 0x01);
        FailIf(definition is not
            {
                Id: EnemyId.ArrowMoblin, SubId: 0x01, Sprites: ["spr_moblin"],
                TileBase: 0, Palette: 1, RadiusY: 6, RadiusX: 6,
                DamageQuarters: 2, Health: 5, Animations.Length: 4
            }, "ENEMY_ARROW_MOBLIN $0c:$01 lost its source palette, combat properties or animations.");

        using var fixture = RoomEntityValidationFixture.ForRoot(this,
            new() { Enemies = database, Random = new OracleRandom(), SaveData = _saveData });
        var manager = fixture.Manager;
        manager.LoadRoom(4, _world.LoadRoom(4, 0xab));
        var moblins = manager.Entities<ArrowMoblinCharacter>();
        int[] subids = [0, 1, 0, 1];
        Vector2[] positions = [new(0x58, 0x48), new(0x98, 0x48),
            new(0x98, 0x68), new(0x58, 0x68)];
        FailIf(moblins.Count != 4 || manager.RoomEnemyCount != 4 ||
            manager.RandomCalls != 256,
            "Crown Dungeon $4:$ab must construct all four counted Arrow Moblins after the placement shuffle.");

        var predictor = new OracleRandom();
        predictor.BeginRoomParse();
        int[] angles = new int[4];
        int[] counters = new int[4];
        for (int i = 0; i < 4; i++)
        {
            FailIf(moblins[i].Record.SubId != subids[i] || moblins[i].Position != positions[i] ||
                moblins[i].State != ArrowMoblinState.Uninitialized,
                $"Crown Dungeon $4:$ab Moblin slot {i} lost its source order, subid or position.");
            predictor.Next(); // enemyStandardUpdate's var3d, before native state 0.
            angles[i] = predictor.Next().Value & 0x18;
            counters[i] = 0x30 + (predictor.Next().Value & 0x3f);
        }
        var originalPosition = _player.Position;
        try
        {
            _player.WarpTo(new Vector2(-0x100, -0x100), recordSafe: false);
            manager.Update(1.0 / 60.0, _player);
            for (int i = 0; i < 4; i++)
            {
                FailIf(moblins[i].State != ArrowMoblinState.Moving ||
                    moblins[i].Angle != angles[i] || moblins[i].Counter != counters[i] ||
                    moblins[i].Position != positions[i] || moblins[i].Health != (subids[i] == 0 ? 3 : 5),
                    $"Crown Dungeon $4:$ab Moblin slot {i} must load subid health then consume scent/direction/duration RNG without moving.");
            }
            FailIf(manager.RandomCalls != 268,
                "Crown Dungeon $4:$ab mixed Moblins changed shared initialization RNG consumption.");

            manager.Update(1.0 / 60.0, _player);
            for (int i = 0; i < 4; i++)
            {
                Vector2 direction = angles[i] switch
                {
                    0 => Vector2.Up, 8 => Vector2.Right,
                    16 => Vector2.Down, _ => Vector2.Left
                };
                FailIf(moblins[i].Position != positions[i] + direction * 0.5f ||
                    moblins[i].Counter != counters[i] - 1,
                    $"Crown Dungeon $4:$ab Moblin slot {i} did not use shared SPEED_80 movement.");
            }
            foreach (var moblin in moblins.Where(moblin => moblin.Record.SubId == 1))
            {
                FailIf(!moblin.TakeSwordHit(moblin.Position + Vector2.Down * 16, 3) ||
                    moblin.Health != 2 || moblin.IsDead,
                    "ENEMY_ARROW_MOBLIN $0c:$01 must survive three damage with two health remaining.");
            }
        }
        finally
        {
            _player.WarpTo(originalPosition, recordSafe: false);
        }
        GD.Print("Validated Crown Dungeon $4:$ab mixed Arrow Moblin subids, ordered construction, source properties, initialization RNG, movement and health.");
    }
}
