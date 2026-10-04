using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonLevers()
    {
        void Step(int count = 1, Vector2 move = default, bool held = false, bool press = false) =>
            StepGameplayUpdates(count, move, held ? ["attack"] : [], press ? ["attack"] : [], batched: true);
        // Independent transcription of leverLavaFiller.s group boundaries.
        // Subid $00 deliberately writes $88 twice; do not deduplicate it.
        var cases = new[] {
            (Room: 0x83, Subid: 0, X: 0xd8, Y: 0x10, Sign: 1, Interval: 4, Source: 0x19, SourceCount: 3,
                Groups: "2a/2b/29/3b/39/4b/4a/49/5b/5a/59/6b/6a/7b/8b/7a/8a/9a/69/99/89/79/98/88/97/78/96/88/87/95/86/85/77/76/75/66/65/56/55/45/35"),
            (Room: 0x84, Subid: 1, X: 0xc8, Y: 0xa0, Sign: -1, Interval: 6, Source: 0x86, SourceCount: 5,
                Groups: "77,78,79/7a,69,68,67,66,76/6a,65,75/64,74/84,85/94,95/83,93/82,92/81,91/71,72/61,62/51,52/41,42/31,32/21,22/11,12/13,23/14,24/34/44/35,45/36/46/47,26/16,27,48/28,38,49/29,39"),
            (Room: 0x7f, Subid: 2, X: 0xa8, Y: 0x10, Sign: 1, Interval: 6, Source: 0x27, SourceCount: 3,
                Groups: "37,38,39/47,48,49/58,59/68/67/77/87/96/85/75/55,64/56/73,45/83,35/25/92,15/81/71/61/51/42/33/13,22/21") };
        var placements = new SkullDungeonDatabase();
        var scripts = new LeverLavaDatabase();
        var profiles = new LeverDatabase();
        var bracelet = new BraceletDatabase().Data;
        FailIf(bracelet.LeverInitialFrames != 1 || bracelet.LeverPullFrames != 40 || bracelet.LeverRestFrames != 20,
            "LIFT_2 lost its initial $01/$00, pull $28/$01, or rest $14/$00 duration/parameter sequence.");
        _inventory.GiveTreasure(TreasureId.Bracelet, 1);
        _inventory.EquipA(TreasureId.Bracelet);
        foreach (var c in cases)
        {
            byte[][] groups = c.Groups.Split('/').Select(group => group.Split(',').Select(value => Convert.ToByte(value, 16)).ToArray()).ToArray();
            byte[] expectedScript = groups.SelectMany(group => group.Append((byte)0)).Append((byte)0).ToArray();
            var records = placements.GetRoomRecords(4, c.Room);
            FailIf(records.Count != 2 || records[0].Id != 0x61 || records[0].SubId != (c.Sign > 0 ? 0x30 : 0x31) ||
                records[0].X != c.X || records[0].Y != c.Y || records[0].Order != 0 ||
                records[1].Id != 0xd8 || records[1].SubId != c.Subid || records[1].Order != 1 ||
                !scripts.Script(c.Subid).Bytes.SequenceEqual(expectedScript) || scripts.Script(c.Subid).Interval != c.Interval,
                $"4:{c.Room:x2} lost native lever/lava order, coordinates, script bytes, duplicate positions, or group boundaries.");
            var profile = profiles.Profile(records[0].SubId);
            FailIf(profile.Behavior != new LeverBehavior(0x40, 0x0a, 5, 1, c.Sign > 0 ? 12 : -13, 16, 0x71, 0x6c) ||
                !profile.Animation.Contains(c.Sign > 0 ? "8,0,0,0" : "8,0,0,64", StringComparison.Ordinal),
                "INTERAC_LEVER lost its upward/downward animation, source offsets, speed, length, or sounds.");
            // Native lever/lava lifecycle is covered by ValidateBraceletLeverRom.
            // Retain independent import goldens, wall gating and room cancellation.
            LoadValidationRoom(4, c.Room);
            _player.WarpTo(new Vector2(c.X, c.Y + 32 * c.Sign));
            _inventory.RefillHealth();
            for (int i = 0; i < 16 && _entities.Entities<EnemyCharacter>().Any(enemy => enemy is not SparkCharacter && enemy.Health > 0); i++)
            {
                _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(_currentRoom.Width, _currentRoom.Height)), _player.Position, damage: 0x7f);
                Step(40);
            }
            FailIf(_entities.Entities<EnemyCharacter>().Any(enemy => enemy is not SparkCharacter && enemy.Health > 0),
                $"Room $4:${c.Room:x2} could not establish the cleared lever fixture.");
            Step();
            var lever = _entities.Entities<LeverRoomEntity>().Single();
            var lava = _entities.Entities<LeverLavaFillerRoomEntity>().Single();
            byte[] sourceLayout = (byte[])_currentRoom.Layout.Clone();
            Vector2 pullDirection = Vector2.Down * c.Sign;
            Step(24, -pullDirection);
            Step(held: true, press: true); Step(held: true);
            FailIf(!lever.Grabbed, $"Room $4:${c.Room:x2} could not grab the lever through its collision approach.");
            if (c.Sign > 0)
            {
                Vector2 wall = new(c.X, 0x28);
                byte tile = _currentRoom.GetMetatile(wall);
                _currentRoom.SetPositionTileAndCollision(wall, tile, 0x0f, 0);
                Step(8, pullDirection, held: true);
                FailIf(lever.PullDistance != 0 || _player.Position.Y != 0x1c,
                    "Lever bypassed updateLinkPositionGivenVelocity's adjacent-wall collision gate.");
                _currentRoom.SetPositionTileAndCollision(wall, tile, null, 0);
            }
            Step();
            Step(24, -pullDirection);
            Step(held: true, press: true); Step(held: true);
            int remaining = 400;
            while ((lever.PullDistance & 0x80) == 0 && remaining-- > 0) Step(move: pullDirection, held: true);
            FailIf(lava.State != 2, "Full lever failed to hand off to the lava script.");
            Step(30, held: true);
            FailIf(lava.Cursor != groups[0].Length + 1, "Could not establish cancellation after the first drying group.");
            LoadValidationRoom(4, 0x91); Step();
            FailIf(_entities.Entities<LeverRoomEntity>().Count != 0 || _entities.Entities<LeverLavaFillerRoomEntity>().Count != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wLever1PullDistance) != 0,
                "Leaving a lever room retained controllers or its shared pull byte.");
            LoadValidationRoom(4, c.Room);
            var reenteredLava = _entities.Entities<LeverLavaFillerRoomEntity>().Single();
            var textSource = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true; Step();
                FailIf(reenteredLava.State != 1 || reenteredLava.Cursor != 0 ||
                    _entities.Entities<LeverRoomEntity>().Single().PullDistance != 0 || !_currentRoom.Layout.SequenceEqual(sourceLayout),
                    "State-zero lava initialization under text retained cancelled tiles, cursor or pull signal.");
            }
            finally { _entities.TextActiveSource = textSource; }
        }
        GD.Print("Validated lever/lava independent import goldens, adjacent-wall pull gate and room cancellation/re-entry initialization.");
    }
}
