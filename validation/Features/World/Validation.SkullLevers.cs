using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonLevers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int count = 1, Vector2 move = default, bool held = false, bool press = false) =>
            StepGameplayUpdates(count, move, held ? ["attack"] : [], press ? ["attack"] : [], batched: true);
        void Wait(int count, bool batch, Vector2 move = default, bool held = false)
        {
            if (batch) Step(count, move, held);
            else for (int i = 0; i < count; i++) Step(move: move, held: held);
        }
        byte[] Underlying() => (byte[])((byte[])typeof(OracleRoomData).GetField("_underlyingLayout", flags)!.GetValue(_currentRoom)!).Clone();
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
            var initialRandom = CaptureOracleRandomForValidation();
            (string Layout, int Calls, Vector2 Link) Run(bool batch)
            {
                RestoreOracleRandomForValidation(initialRandom);
                LoadValidationRoom(4, c.Room);
                _player.WarpTo(new Vector2(c.X, c.Y + 32 * c.Sign));
                _inventory.RefillHealth();
                // Isolate the mechanism after ordinary combat/death dispatch.
                // Spark remains active: its native wall movement consumes no RNG.
                for (int i = 0; i < 16 && _entities.Entities<EnemyCharacter>().Any(enemy => enemy is not SparkCharacter && enemy.Health > 0); i++)
                {
                    _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(_currentRoom.Width, _currentRoom.Height)), _player.Position, damage: 0x7f);
                    Step(40);
                }
                FailIf(_entities.Entities<EnemyCharacter>().Any(enemy => enemy is not SparkCharacter && enemy.Health > 0),
                    $"4:{c.Room:x2} could not establish the cleared ordinary-enemy lever fixture.");
                Step();
                var lever = _entities.Entities<LeverRoomEntity>().Single();
                var connection = _entities.Entities<LeverConnectionRoomEntity>().Single();
                var lava = _entities.Entities<LeverLavaFillerRoomEntity>().Single();
                FailIf(lever.Position != new Vector2(c.X, c.Y) || lever.PullDistance != 0 || lava.State != 1 ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wLever2PullDistance) != 0,
                    "Room initialization retained a stale shared lever signal or lava phase.");
                byte[] sourceLayout = (byte[])_currentRoom.Layout.Clone();
                byte[] sourceUnderlying = Underlying();
                Vector2 pullDirection = Vector2.Down * c.Sign;
                void Grab(bool releaseDuringInitialization = false)
                {
                    Step(80, -pullDirection);
                    FailIf(_currentRoom.IsSolid(_player.Position), "Lever approach put Link inside native solid room geometry.");
                    Step(held: true, press: true);
                    FailIf(!lever.Grabbed || _bracelet.State != BraceletState.PullingInteraction ||
                        _player.Position != new Vector2(c.X, lever.Position.Y + (c.Sign > 0 ? 12 : -13)),
                        $"4:{c.Room:x2} actual bracelet input could not grab from its collision-limited approach; Link={_player.Position}, lever={lever.Position}, bracelet={_bracelet.State}.");
                    Step(held: !releaseDuringInitialization);
                    FailIf(!lever.Grabbed || lever.PullDistance != 0,
                        "Bracelet state 2 checked the item button or moved the lever before loading state 5's LIFT_2 animation.");
                    FailIf(!_player.BraceletLiftCollisionsDisabled ||
                        _player.ApplyEnemyContactDamage(_player.Position + Vector2.Left * 16, 1),
                        "Bracelet's lever branch re-enabled Link collisions before the held parent was released.");
                }
                Grab(releaseDuringInitialization: true);
                if (c.Sign > 0)
                {
                    Vector2 wall = new(c.X, 0x28);
                    byte tile = _currentRoom.GetMetatile(wall);
                    _currentRoom.SetPositionTileAndCollision(wall, tile, 0x0f, 0);
                    Wait(8, batch, pullDirection, held: true);
                    FailIf(lever.PullDistance != 0 || _player.Position.Y != 0x1c,
                        "Lever bypassed updateLinkPositionGivenVelocity's adjacent-wall collision gate.");
                    _currentRoom.SetPositionTileAndCollision(wall, tile, null, 0);
                    Step(held: true);
                }
                Wait(8, batch, pullDirection, held: true);
                int partial = lever.PullDistance;
                Wait(8, batch, held: true);
                FailIf(partial != 2 || lever.PullDistance != partial || lava.State != 1,
                    "Partial/pause pull changed the lava signal or failed to retain the lever position.");
                Step();
                FailIf(lever.PullDistance != partial || lever.Grabbed || _player.BraceletLiftCollisionsDisabled,
                    "Release must restore Link collisions and change lever state before retraction starts.");
                Wait(8, batch);
                FailIf(lever.PullDistance != 0 || lever.Position.Y != c.Y || lava.State != 1,
                    "A partial pull did not retract without starting the lava script.");
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    Grab();
                    int moveSounds = _sound.PlayRequestsFor(SoundId.SndMoveBlock);
                    int fullSounds = _sound.PlayRequestsFor(SoundId.SndOpenChest);
                    int solveSounds = _sound.PlayRequestsFor(SoundId.SndSolvePuzzle);
                    // Six 20-update rests occur before reaching 256/253
                    // quarter-pixel movement updates (down/up respectively).
                    int pullUpdates = c.Sign > 0 ? 376 : 373;
                    Wait(40, batch, pullDirection, held: true);
                    FailIf(lever.PullDistance != 10, "LIFT_2 first pull phase did not advance exactly ten pixels.");
                    Wait(20, batch, pullDirection, held: true);
                    FailIf(lever.PullDistance != 10, "Lever moved during LIFT_2's 20-update rest while direction stayed held.");
                    Wait(pullUpdates - 61, batch, pullDirection, held: true);
                    FailIf(lever.PullDistance != 0x3f || lava.State != 1,
                        $"4:{c.Room:x2} full-extension flag arrived before update {pullUpdates}; distance={lever.PullDistance:x2}.");
                    Step(move: pullDirection, held: true);
                    FailIf(lever.PullDistance != 0xc0 || lever.Position.Y != c.Y + c.Sign * 64 || connection.Phase != 4 ||
                        connection.Position != new Vector2(c.X, c.Y + c.Sign * 32) || lava.State != 2 || lava.Counter != 30,
                        "Lever full flag/connection and the following $d8 dispatch did not share the exact completion update.");
                    FailIf(_sound.PlayRequestsFor(SoundId.SndMoveBlock) != moveSounds + 7 ||
                        _sound.PlayRequestsFor(SoundId.SndOpenChest) != fullSounds + 1 ||
                        _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != solveSounds + 1,
                        "Pull/rest animation did not reset the move-sound latch, or full extension lost its ordered open/solve sounds.");
                    var textSource = _entities.TextActiveSource;
                    try
                    {
                        _entities.TextActiveSource = () => true;
                        Wait(5, batch, held: true);
                        FailIf(lava.Counter != 30 || lava.State != 2 || lever.PullDistance != 0xc0,
                            "Initialized lever/lava advanced during text-active interaction freeze.");
                    }
                    finally { _entities.TextActiveSource = textSource; }
                    for (int i = 0; i < c.SourceCount; i++)
                        FailIf(_currentRoom.Layout[c.Source + i] != sourceLayout[c.Source + i] + 6 ||
                            Underlying()[c.Source + i] != sourceUnderlying[c.Source + i],
                            "Lava-source visual toggle changed the wrong tiles or the underlying buffer.");
                    byte[] expected = (byte[])_currentRoom.Layout.Clone();
                    byte[] expectedUnderlying = Underlying();
                    for (int g = 0; g < groups.Length; g++)
                    {
                        Wait((g == 0 ? 30 : c.Interval) - 1, batch, held: true);
                        FailIf(!_currentRoom.Layout.SequenceEqual(expected), "Lava drying group ran before its exact counter boundary.");
                        Step(held: true);
                        foreach (int p in groups[g]) { expected[p] = 1; expectedUnderlying[p] = 1; }
                        FailIf(!_currentRoom.Layout.SequenceEqual(expected) || !Underlying().SequenceEqual(expectedUnderlying),
                            $"4:{c.Room:x2} drying group {g} did not preserve its source positions and both buffers.");
                    }
                    Wait(c.Interval - 1, batch, held: true);
                    FailIf(lava.State != 2, "Lava skipped the final empty-group delay.");
                    Step(held: true);
                    FailIf(lava.State != 3 || lava.Counter != c.Interval, "Empty drying group did not enter the retraction wait with counter2 retained.");
                    Wait(8, batch, held: true);
                    FailIf(lava.State != 3, "Dried floor refilled while Link still held the full lever.");
                    Step();
                    FailIf(lever.PullDistance != 0xc0, "Release retracted the lever on the state-change update.");
                    Step(3);
                    FailIf(lever.PullDistance != (c.Sign > 0 ? 0x3f : 0xc0),
                        "Upward retraction lost the full-distance flag before fractional Y crossed its first pixel.");
                    FailIf(_sound.PlayRequestsFor(SoundId.SndOpenChest) != fullSounds + (c.Sign > 0 ? 1 : 4),
                        "Upward retraction did not replay updatePullOffset's full-extension sound on its first three fractional updates.");
                    int retractUpdates = c.Sign > 0 ? 253 : 256;
                    Wait(retractUpdates - 4, batch);
                    FailIf(lever.PullDistance == 0 || lava.State != 3, "Lava refilled before the lever returned to its native zero byte.");
                    Step();
                    FailIf(lever.PullDistance != 0 || lava.State != 4 || lava.Counter != c.Interval,
                        "The first zero pull-byte dispatch did not start lava refill with the retained interval.");
                    for (int i = 0; i < c.SourceCount; i++) expected[c.Source + i] = sourceLayout[c.Source + i];
                    // Independently execute bank0 getRandomNumber arithmetic,
                    // including the repeated $88 write, without using the imported script.
                    var before = _random.CaptureState();
                    int rng1 = before.Rng1, rng2 = before.Rng2, calls = before.Calls;
                    for (int g = 0; g < groups.Length; g++)
                    {
                        Wait(c.Interval - 1, batch);
                        FailIf(!_currentRoom.Layout.SequenceEqual(expected), "Lava refill group ran before its source counter boundary.");
                        foreach (int p in groups[g])
                        {
                            rng2 = ((((rng2 << 8) | rng1) * 3) & 0xffff) >> 8;
                            rng1 = (rng1 + rng2) & 0xff;
                            expected[p] = expectedUnderlying[p] = (byte)(0x61 + (rng1 & 3));
                            calls++;
                        }
                        Step();
                        FailIf(!_currentRoom.Layout.SequenceEqual(expected) || !Underlying().SequenceEqual(expectedUnderlying) ||
                            _random.Calls != calls,
                            $"4:{c.Room:x2} refill group {g} lost ordered RNG consumption or tile buffers; calls={_random.Calls}/{calls}.");
                    }
                    Wait(c.Interval, batch);
                    FailIf(lava.State != 1, "Final empty lava group did not return the mechanism to its repeatable waiting state.");
                }
                var result = (Convert.ToHexString(_currentRoom.Layout), _random.Calls, _player.Position);
                Grab();
                Wait(c.Sign > 0 ? 376 : 373, batch, pullDirection, held: true);
                Wait(30, batch, held: true);
                FailIf(lava.State != 2 || lava.Cursor != groups[0].Length + 1,
                    "Could not establish an active lava-script cancellation after its first drying group.");
                LoadValidationRoom(4, 0x91);
                Step();
                FailIf(_entities.Entities<LeverRoomEntity>().Count != 0 || _entities.Entities<LeverLavaFillerRoomEntity>().Count != 0 ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wLever1PullDistance) != 0,
                    "Leaving a lever room retained its controllers or shared pull byte.");
                LoadValidationRoom(4, c.Room);
                var reenteredLava = _entities.Entities<LeverLavaFillerRoomEntity>().Single();
                var textSourceOnEntry = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step();
                    FailIf(reenteredLava.State != 1 || reenteredLava.Cursor != 0 ||
                        _entities.Entities<LeverRoomEntity>().Single().PullDistance != 0 ||
                        !_currentRoom.Layout.SequenceEqual(sourceLayout),
                        "Lava state zero did not initialize under text, or re-entry retained cancelled tiles, script position, or pull signal.");
                }
                finally { _entities.TextActiveSource = textSourceOnEntry; }
                return result;
            }
            var single = Run(false);
            var batched = Run(true);
            FailIf(single != batched, $"4:{c.Room:x2} individual and batched application updates diverged across two lever/lava cycles.");
        }
        GD.Print("Validated Skull levers/lava: source profiles and all ordered groups, actual collision approaches and bracelet input, partial/pause/repeat pulls, upward fractional timing, connection phases, drying/refill counters, both tile buffers, and shared ordered RNG under individual/batched application updates.");
    }
}
