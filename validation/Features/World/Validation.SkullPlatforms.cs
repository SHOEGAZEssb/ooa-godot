using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonPlatforms()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 move = default, bool jump = false)
        {
            input.CaptureForValidation(jump ? ["attack"] : [], jump ? ["attack"] : [], move);
            scheduler.Advance(count / 60.0, update);
        }
        void Wait(int count, bool batch)
        {
            if (batch) Step(count);
            else for (int i = 0; i < count; i++) Step();
        }
        var data = new MovingPlatformDatabase();
        string[] sourcePrograms = [
            "00:08,08:60,00:08,0a:a0,00:08,08:a0,04:02",
            "00:08,08:20,00:08,0a:c0,00:08,08:c0,04:02",
            "00:08,08:40,00:08,0a:a0,00:08,08:a0,04:02",
            "00:08,09:20,00:08,0b:c0,00:08,09:c0,04:02",
            "00:08,0a:60,00:08,08:60,04:00",
            "00:08,0b:20,00:08,09:a0,00:08,0b:a0,04:02" ];
        for (int dungeon = 2; dungeon <= 4; dungeon++)
        for (int index = 0; index < sourcePrograms.Length; index++)
            FailIf(string.Join(',', data.Script(dungeon, index).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != sourcePrograms[index],
                $"Dungeon ${dungeon:x2} platform script ${index:x2} lost source aliases, first leg, loop target, or waits.");
        FailIf(string.Join(',', data.Script(1, 0).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != "00:08,08:80,00:08,0a:80,04:00" ||
            string.Join(',', data.Script(1, 1).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != "00:08,0b:40,00:08,09:80,00:08,0b:80,04:02",
            "Dungeon 1 platforms inherited Skull Dungeon's different route table.");
        var cases = new[] {
            (Room: 0x6c, Subid: 0x03, X: 0xa0, Y: 0x58, RX: 16, RY: 8),
            (Room: 0x6c, Subid: 0x08, X: 0x68, Y: 0x28, RX: 8, RY: 8),
            (Room: 0x74, Subid: 0x11, X: 0x70, Y: 0x40, RX: 8, RY: 16),
            (Room: 0x75, Subid: 0x1a, X: 0xa8, Y: 0x28, RX: 8, RY: 24),
            (Room: 0x75, Subid: 0x23, X: 0x68, Y: 0x48, RX: 16, RY: 8),
            (Room: 0x75, Subid: 0x29, X: 0x68, Y: 0x90, RX: 8, RY: 16) };
        var skull = new SkullDungeonDatabase();
        foreach (var c in cases)
        {
            var placed = skull.GetRoomRecords(4, c.Room).Single(r => r.SubId == c.Subid && r.Id == 0x79);
            FailIf(placed.X != c.X || placed.Y != c.Y, $"4:{c.Room:x2} platform ${c.Subid:x2} lost its source placement.");
            (Vector2 Position, int Counter, int Command) Run(bool batch)
            {
                LoadValidationRoom(4, c.Room);
                _player.WarpTo(c.Room switch { 0x6c => new Vector2(40, 56), 0x74 => new Vector2(72, 72), _ => new Vector2(200, 40) });
                _inventory.RefillHealth();
                FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                    "Platform motion fixture must start on the room's actual safe shore.");
                // Isolate script timing from enemy damage; Link remains on the
                // room's ordinary safe spawn throughout this motion-only audit.
                _player.SetBraceletLiftCollisionsDisabled(true);
                var platform = _entities.Entities<MovingPlatformRoomEntity>().Single(p => p.Script == c.Subid >> 3);
                FailIf(platform.Position != new Vector2(c.X, c.Y) || platform.CollisionRadii != new Vector2(c.RX, c.RY) ||
                    platform.CurrentTexture.GetWidth() < c.RX * 2 || platform.CurrentTexture.GetHeight() < c.RY * 2,
                    $"4:{c.Room:x2} platform lost source geometry or clipped its long OAM axis.");
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step();
                    FailIf(platform.Counter != 8 || platform.Command != 1 || !platform.Visible || platform.LinkRiding,
                        "Moving-platform state zero did not initialize its wait under text without claiming Link.");
                    Step(8);
                    FailIf(platform.Counter != 8, "Initialized moving platform advanced during text.");
                }
                finally { _entities.TextActiveSource = text; }
                Vector2 expected = platform.PrecisePosition;
                string[] native = sourcePrograms[c.Subid >> 3].Split(',');
                int pc = 1;
                for (int leg = 0; leg < 5; leg++)
                {
                    var instrumentSource = _entities.PlayingInstrumentSource;
                    if (leg == 0) _entities.PlayingInstrumentSource = () => 1;
                    Wait(7, batch);
                    FailIf(platform.PrecisePosition != expected || platform.Counter != 1 || platform.Moving,
                        "Moving platform consumed wait8 too early.");
                    Step();
                    var move = native[pc++].Split(':');
                    int opcode = Convert.ToInt32(move[0], 16), duration = Convert.ToInt32(move[1], 16);
                    Vector2 direction = new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left }[opcode - 8];
                    FailIf(!platform.Moving || platform.Counter != duration || platform.PrecisePosition != expected,
                        "Wait8's zero update did not select movement without applying speed yet.");
                    if (leg == 0)
                    {
                        try
                        {
                            Wait(4, batch);
                            FailIf(platform.Counter != duration || platform.PrecisePosition != expected || !_entities.PlayerRidingObject,
                                "Instrument must permit wait completion, freeze the selected movement, and retain nonzero riding support.");
                        }
                        finally { _entities.PlayingInstrumentSource = instrumentSource; }
                    }
                    Wait(duration - 1, batch);
                    expected += direction * ((duration - 1) * 0.5f);
                    FailIf(platform.Counter != 1 || platform.PrecisePosition != expected,
                        $"4:{c.Room:x2}/${c.Subid:x2} leg{leg} batch={batch}: platform={platform.PrecisePosition}/{expected}, counter={platform.Counter}, Link={_player.Position}, health={_player.HealthQuarters}, dying={_player.IsDying}, room={_currentRoom.Id:x2}, text={_dialogue.IsOpen}.");
                    Step();
                    expected += direction * 0.5f;
                    if (native[pc].StartsWith("04:", StringComparison.Ordinal)) pc = Convert.ToInt32(native[pc].Split(':')[1], 16);
                    pc++; // Zero-update script already executes the next wait.
                    FailIf(platform.Moving || platform.Counter != 8 || platform.PrecisePosition != expected,
                        "Moving platform added a dispatch between final movement, jump, and the next wait.");
                }
                var result = (platform.PrecisePosition, platform.Counter, platform.Command);
                _player.SetBraceletLiftCollisionsDisabled(false);
                LoadValidationRoom(4, 0x91);
                Step();
                FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 0, "Room cancellation retained a moving platform.");
                return result;
            }
            FailIf(Run(false) != Run(true), $"4:{c.Room:x2} platform ${c.Subid:x2} diverged under batched application updates.");
        }

        // Real Feather boarding through 4:74's west shore; the floor and lava
        // retain their native collision. No placement inside a platform is used.
        LoadValidationRoom(4, 0x74);
        _player.WarpTo(new Vector2(80, 68));
        FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
            "4:74's source shore is not a safe Feather approach.");
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.EquipA(InventoryState.ItemFeather);
        var rider = _entities.Entities<MovingPlatformRoomEntity>().Single();
        // Clear the Keese through combat/death dispatch before the long route
        // wait; this regression isolates boarding from enemy combat.
        for (int i = 0; i < 8 && _entities.Entities<EnemyCharacter>().Any(e => e.Health > 0); i++)
        {
            _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(_currentRoom.Width, _currentRoom.Height)), _player.Position, damage: 0x7f);
            Step(40);
        }
        Step();
        int initialBoardingWait = 0;
        while ((!rider.Moving || rider.Angle != 0 || rider.PrecisePosition.Y != 72) && initialBoardingWait++ < 700) Step();
        FailIf(initialBoardingWait >= 700, "Source platform never reached the shore's safe boarding window.");
        Step(move: Vector2.Right, jump: true);
        bool rodeAirborne = false;
        for (int i = 0; i < 26; i++)
        {
            Step(move: Vector2.Right);
            rodeAirborne |= rider.LinkRiding && _player.TopDownAirborne;
        }
        FailIf(!rodeAirborne || !rider.LinkRiding || _player.IsDrowning,
            $"Actual Feather input did not board 4:74's moving platform while airborne; Link={_player.Position}, platform={rider.Position}, riding={rider.LinkRiding}.");
        // A newly allocated overlapping platform cannot steal or double the
        // earlier source object's displacement in the same interaction pass.
        var overlap = _entities.Spawn<MovingPlatformRoomEntity>(new MovingPlatformSpawn(rider.Position, 0x11));
        for (int i = 0; i < 20; i++)
        {
            Vector2 link = _player.PrecisePosition, platform = rider.PrecisePosition;
            Step();
            FailIf(!rider.LinkRiding || overlap.LinkRiding || _player.PrecisePosition.Y - link.Y != rider.PrecisePosition.Y - platform.Y,
                $"Overlapping platform update{i}: owners={rider.LinkRiding}/{overlap.LinkRiding}, Link={link}->{_player.PrecisePosition}, platform={platform}->{rider.PrecisePosition}, air={_player.TopDownAirborne}, drowning={_player.IsDrowning}, knockback={_player.KnockbackFrames}.");
        }
        var instrument = _entities.PlayingInstrumentSource;
        try
        {
            _entities.PlayingInstrumentSource = () => 1;
            Vector2 position = rider.PrecisePosition;
            int counter = rider.Counter;
            Step(5);
            FailIf(rider.LinkRiding || overlap.LinkRiding || !_entities.PlayerRidingObject || rider.PrecisePosition != position || rider.Counter != counter,
                "Instrument sentinel did not block platform claiming and freeze moving substate/counter.");
        }
        finally { _entities.PlayingInstrumentSource = instrument; }
        Step();
        FailIf(!rider.LinkRiding || overlap.LinkRiding, "Ordered platform ownership did not resume after the instrument sentinel cleared.");
        int returnWait = 0;
        while ((rider.Angle != 0x10 || _player.Position.Y < 64 || _player.TopDownAirborne) && returnWait++ < 500) Step();
        FailIf(returnWait >= 500 || !rider.LinkRiding,
            $"Platform return wait{ returnWait}: Link={_player.Position}, platform={rider.Position}, owner={rider.LinkRiding}/{overlap.LinkRiding}, health={_player.HealthQuarters}, drowning={_player.IsDrowning}, air={_player.TopDownAirborne}.");
        Step(move: Vector2.Left, jump: true);
        for (int i = 0; i < 80 && _player.TopDownAirborne; i++) Step(move: Vector2.Left);
        FailIf(rider.LinkRiding || _player.IsDrowning || _currentRoom.IsSolid(_player.Position) ||
            _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
            $"Feather return failed to reach the original west shore; Link={_player.Position}, riding={rider.LinkRiding}.");
        Vector2 shore = new(80, 68);
        for (int i = 0; i < 100 && _player.Position.DistanceTo(shore) > 0.8f; i++)
        {
            Vector2 delta = shore - _player.Position;
            Step(move: Math.Abs(delta.X) > 0.8f ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y)));
        }
        FailIf(_player.Position.DistanceTo(shore) > 0.8f, $"Link could not walk back to the Feather launch point on native shore geometry: {_player.Position}.");
        int boardingWait = 0;
        while ((!rider.Moving || rider.Angle != 0 || rider.PrecisePosition.Y != 72) && boardingWait++ < 700) Step();
        FailIf(boardingWait >= 700, "Repeating platform route never returned to the source boarding window.");
        Step(move: Vector2.Right, jump: true);
        for (int i = 0; i < 26; i++) Step(move: Vector2.Right);
        FailIf(!rider.LinkRiding || overlap.LinkRiding || !_player.TopDownAirborne,
            "Repeated Feather input from the original shore failed to reacquire the first source platform.");
        LoadValidationRoom(4, 0x91);
        Step();
        LoadValidationRoom(4, 0x74);
        FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 1 || _entities.PlayerRidingObject,
            "Leaving/re-entering retained the old ride owner or dynamically spawned platform.");
        GD.Print("Validated Skull moving platforms: six source placements/size visuals, dungeon-specific scripts and aliases, exact wait/move/loop updates, individual/batched execution, native Feather boarding, airborne carry, overlapping ownership, instrument freeze, text initialization, and cancellation/re-entry.");
    }
}
