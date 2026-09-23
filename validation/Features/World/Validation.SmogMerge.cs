using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogMerge()
    {
        // SUB/ADD/CP in @checkEnemiesCloseEnoughToMerge wraps each byte;
        // fractions never participate. Wall difference allows +/-3, XY +/-4.
        for (int wall = -5; wall <= 5; wall++)
        for (int x = -6; x <= 6; x++)
        for (int y = -6; y <= 6; y++)
        {
            bool expected = Math.Abs(wall) <= 3 && Math.Abs(x) <= 4 && Math.Abs(y) <= 4;
            bool actual = SmogCloudMerge.CloseEnough(wall & 255,0,
                new((x & 255) + 0.75f,(y & 255) + 0.25f),new(0.125f,0.875f));
            FailIf(actual != expected, $"Smog merge byte boundaries changed at wall={wall}, X={x}, Y={y}.");
        }
        var record = new EnemyDatabase().ImportedEnemy(0x7c,0);
        SmogCharacter Cloud(Vector2 position,int subid = 2)
        {
            var actor = new SmogCharacter();
            actor.InitializeSmallCloud(record,subid,0,position,1,_ => 0);
            actor.UpdateSmallCloud(0,() => {},_ => {},_ => {},() => {},() => 0);
            return actor;
        }
        foreach (int pair in new[] { 0,1,2 })
        {
            // Pair priority: first/second, first/third, second/third.
            var a = Cloud(new(40,40));
            var b = Cloud(pair == 2 ? new(80,80) : pair == 1 ? new(120,104) : new(42,42),0x82);
            var c = Cloud(pair == 2 ? new(82,82) : new(43,43));
            var first = pair == 2 ? b : a;
            var second = pair == 0 ? b : c;
            int highBit = (first.SubId ^ second.SubId) & 0x80;
            SmogEnemySpawn? replacement = null;
            FailIf(!SmogCloudMerge.TryMerge([a,b,c],2,spawn =>
                {
                    FailIf(first.SubId != highBit || second.SubId != 6 || first.IsDead || second.IsDead,
                        "Merge allocation must observe the temporary first subid and second deletion marker while both slots remain enabled.");
                    replacement = spawn;
                }), "Expected ordered Smog pair to merge.");
            FailIf(replacement != new SmogEnemySpawn(first.Position.Floor(),highBit | 3,2,1) ||
                first.SubId != 6 || second.SubId != 6 || first.IsDead || second.IsDead,
                "Merged Smog must copy first high position/direction, XOR bit7, phase and defer both deletions.");
            a.Free(); b.Free(); c.Free();
        }
        {
            var clouds = new[] { Cloud(new(24,24)),Cloud(new(72,72)),Cloud(new(120,104)),Cloud(new(24,24)) };
            FailIf(SmogCloudMerge.TryMerge(clouds,0,_ => throw new InvalidOperationException("Fourth cloud must not be scanned.")),
                "INTERAC$33 only considers the first three matching ENEMY slots.");
            foreach (var cloud in clouds) cloud.Free();
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(8,8));
            for (int y = 8; y < 128; y += 16)
                for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x,y),0x0c,0,0);
            void Step(int count = 1)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            var first = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),2,2,1));
            var second = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(74,74),0x82,2,3));
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120,104),2));
            Step();
            FailIf(!_entities.TryMergeSmogClouds(2) || _entities.RoomEnemyCount != 4 || first.IsDead || second.IsDead,
                "Merging must allocate a fourth counted slot before deleting either original.");
            var merged = (SmogCharacter)_entities.EntityAdapters<SmogRoomEntity>().Single(entity =>
                ((SmogCharacter)entity.Node).SubId == 0x83).Node;
            FailIf(merged.Position != new Vector2(72,72) || merged.Counter2 != 5 || merged.State != 0,
                "Replacement must begin at first cloud's byte position with state0/counter2=5.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true; Step();
                FailIf(first.IsDead || second.IsDead || _entities.RoomEnemyCount != 4 || merged.Counter2 != 4,
                    "Frozen initialized subid$06 must await eligibility while the state0 replacement initializes.");
            }
            finally { _entities.TextActiveSource = text; }
            Step();
            FailIf(!first.IsDead || !second.IsDead || _entities.RoomEnemyCount != 2 || merged.Counter2 != 3,
                "Next eligible enemy pass must delete both originals exactly once before the replacement's update.");
            Step(3);
            FailIf(merged.State != 8 || merged.SubId != 4 || _entities.RoomEnemyCount != 2,
                "Replacement's fifth initialization must observe the post-deletion count and become large.");
        }
        _entities.Clear();
        GD.Print("Validated Smog merge byte bounds, pair/slot ordering, allocation side effects and deferred deletion in single and batched gameplay updates.");
    }
}
