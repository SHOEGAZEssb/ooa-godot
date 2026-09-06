using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriUnmountedHole()
    {
        _saveData.WriteWramByte(0xc647, 0x80);
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(72, 56), 0xf3, 0, 0);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        _player.SetLocalRespawnCoordinates(new Vector2(40, 40));
        int health = _player.HealthQuarters;
        var dimitri = _entities.Spawn<DimitriCompanionRoomEntity>(
            new DimitriCompanionSpawn(new Vector2(72, 56), 2, 0, 0x2a));
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Hazard || dimitri.LinkRiding || _player.CompanionRideActive,
            "Unmounted Dimitri entering a hole must not take over Link's companion pose.");
        for (int update = 0; update < 240 && dimitri.Phase == DimitriPhase.Hazard; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Waiting || dimitri.PrecisePosition != new Vector2(40, 40) ||
            _player.HealthQuarters != health || _player.PrecisePosition != new Vector2(8, 8),
            "companionRespawn damaged or moved unmounted Link, or failed to restore Dimitri's local respawn coordinates.");
        GD.Print("Validated unmounted Dimitri hole animation and respawn without Link damage or forced mounting.");
    }

    private void ValidateDimitriCliff()
    {
        _saveData.WriteWramByte(0xc647, 0x80);
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(72, 72), 0xd4, 3, 0);
        var dimitri = _entities.Spawn<DimitriCompanionRoomEntity>(
            new DimitriCompanionSpawn(new Vector2(72, 64), 2, 0, 0x2a, Riding: true));
        CompanionRuntimeState.Begin(_entities.RuntimeState, 0x0c, 0x2a, new Vector2(72, 64), 2);
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: ["move_down"], justPressed: [], movement: Vector2.Down));
        try
        {
            _entities.Update(1.0 / 60.0, _player);
            _entities.Update(1.0 / 60.0, _player);
        }
        finally { Input.EndOriginalUpdate(); }
        FailIf(dimitri.Phase != DimitriPhase.CliffJump || dimitri.Counter != 20,
            "Dimitri did not recognize the native downward vine-top cliff after the angle-change update.");
        Vector2 start = dimitri.PrecisePosition;
        _sound.ClearPlayRequestAudit();
        for (int update = 0; update < 19; update++) _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.PrecisePosition != start || dimitri.Counter != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 0,
            "Dimitri cliff anticipation moved or played its jump sound before counter $14 expired.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.PrecisePosition != start || _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 1,
            "Dimitri cliff counter-zero update must play the sound without moving yet.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.PrecisePosition.Y != start.Y + 2 || dimitri.ZFixed >= 0,
            "Dimitri cliff launch did not apply SPEED_200 and the source vertical impulse.");
        for (int update = 0; update < 30 && dimitri.Phase == DimitriPhase.CliffJump; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Riding,
            "Dimitri did not leave cliff state after the rear collision probes crossed the wall.");
        GD.Print("Validated Dimitri vine-top cliff, $14 anticipation, counter-zero sound, fixed-point launch and rear-wall exit.");
    }

    private void ValidateDimitriFlute()
    {
        var data = new FluteDatabase();
        var collisions = new DimitriDatabase();
        FailIf(collisions.AcceptsMouthCollision(0x16) || !collisions.AcceptsMouthCollision(0x1b) ||
            !collisions.AcceptsMouthCollision(0x1d),
            "Dimitri mouth lost the Beamos/rock-cover exclusion or Spiny Beetle/Armos collision gates.");
        FailIf(data.Duration(0) != 95 || data.Duration(2) != 255,
            "Flute terminal parameters no longer delimit the source $5f/$ff update songs.");
        _saveData.WriteWramByte(0xc647, 0x80);
        _saveData.WriteWramByte(0xc6b5, 2);
        _inventory.GiveTreasure(InventoryState.ItemFlute, 0x0c);
        LoadValidationRoom(0, 0x2a);
        FailIf(!data.Callable(0x2a), "Flute fixture 0:2a must be in the source callable-room bitmap.");
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        _player.WarpTo(new Vector2(72, 72), recordSafe: false);
        int notes = _harp.NoteSpawnCount, random = _entities.RandomCalls;
        _inventory.EquipB(InventoryState.ItemFlute);
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: ["item"], justPressed: ["item"], movement: Vector2.Zero));
        try { _player.AdvanceApplicationUpdate(); }
        finally { Input.EndOriginalUpdate(); }
        FailIf(!_harp.IsPlayingFlute || !_player.IsUsingHarp || !_player.HarpPoseActive,
            "Equipped Dimitri flute did not allocate the shared instrument parent and native Link pose.");
        for (int update = 2; update <= 255; update++)
        {
            _player.AdvanceHarpForValidation(1);
            FailIf(update < 255 && !_player.IsUsingHarp,
                $"Dimitri flute finished before source update $ff (update {update}).");
        }
        FailIf(_player.IsUsingHarp || _player.HarpPoseActive || _harp.IsPlayingFlute,
            "Dimitri flute retained the instrument parent or Link pose after update $ff.");
        FailIf(_harp.NoteSpawnCount != notes + 7 || _entities.RandomCalls != random + 7,
            "Dimitri's flute must consume one shared RNG byte at each of seven $20-update note boundaries.");
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        FailIf(dimitri.Phase != DimitriPhase.FlutePending || dimitri.PrecisePosition != new Vector2(72, -8),
            "Flute entrance search did not prefer the two clear top-edge tiles at Link's column.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.FluteEntering || dimitri.Counter != 60,
            "Dimitri flute entry did not initialize its separate sixty-update movement counter.");
        for (int update = 0; update < 59; update++) _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.FluteEntering || dimitri.Counter != 1,
            $"Dimitri ended a clear flute entrance before update $3c: {dimitri.Phase}, counter {dimitri.Counter}, position {dimitri.PrecisePosition}.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Waiting || dimitri.Direction != 2,
            "Dimitri did not become available after the flute entrance.");
        GD.Print("Validated Dimitri flute source duration, note RNG, ordered entrance search and $3c movement boundary.");
    }

    private void ValidateDimitriCarrying()
    {
        _saveData.WriteWramByte(0xc647, 0x80);
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        var dimitri = _entities.Spawn<DimitriCompanionRoomEntity>(
            new DimitriCompanionSpawn(new Vector2(72, 56), 2, 0, 0x2a));
        _player.WarpTo(new Vector2(72, 72), recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(!_bracelet.TryUse(_player, primaryButton: false) || !_player.IsCarryingObject ||
            dimitri.Phase != DimitriPhase.Carried || (_saveData.ReadWramByte(0xc649) & 4) == 0 ||
            dimitri.AnimationIndex != 0x18,
            "Dimitri did not enter native carried state through the shared Bracelet parent.");
        for (int update = 0; update < 40; update++)
        {
            _bracelet.Update(_player, Vector2.Zero, false, true, false);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(dimitri.ZFixed >= 0 || dimitri.LinkRiding,
            "Carried Dimitri lost his held Z or incorrectly mounted Link.");
        int heldZ = dimitri.ZFixed;
        Vector2 scrollPosition = _player.PrecisePosition;
        dimitri.BeginScreenTransition(_rooms.CurrentRoom);
        dimitri.SetScreenTransitionPosition(scrollPosition, new Vector2(-32, 0), _player);
        FailIf(dimitri.PrecisePosition.X != scrollPosition.X + CarriedObjectMotion.HeldOffset(_player).X ||
            _player.PrecisePosition != scrollPosition || dimitri.ZFixed != heldZ,
            "Carried Dimitri mixed camera scroll into world position or changed held height.");
        dimitri.FinishScreenTransition(scrollPosition, _player);
        FailIf(!_player.IsCarryingObject || dimitri.Phase != DimitriPhase.Carried || dimitri.LinkRiding,
            "Carried Dimitri lost the Bracelet pose or mounted Link after a screen transition.");
        FailIf(!dimitri.TryUseBracelet(_player, Vector2I.Right) || _player.IsCarryingObject,
            "Dimitri could not be thrown from the shared held state.");
        Vector2 start = dimitri.PrecisePosition;
        _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Thrown || dimitri.PrecisePosition.X <= start.X,
            "Thrown Dimitri did not consume the Bracelet's fixed-point horizontal velocity.");
        for (int update = 0; update < 200 && dimitri.Phase is DimitriPhase.Thrown or DimitriPhase.ThrownLanding; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase is not (DimitriPhase.Waiting or DimitriPhase.AwaitingDistance) ||
            dimitri.ZFixed != 0 || dimitri.PrecisePosition.X > 155,
            "Thrown Dimitri did not settle at the native small-group boundary.");
        _bracelet.Interrupt(_player, discard: true);
        GD.Print("Validated shared Bracelet lift, Dimitri curled pose/tutorial flag, source throw motion, bounce/landing and group-zero boundary.");
    }

    private void ValidateDimitriWaterReturn()
    {
        _saveData.WriteWramByte(0xc647, 0x80);
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), x >= 88 ? (byte)0xfe : (byte)0, 0, 0);
        var dimitri = _entities.Spawn<DimitriCompanionRoomEntity>(
            new DimitriCompanionSpawn(new Vector2(72, 56), 2, 0, 0x2a));
        _player.WarpTo(new Vector2(72, 72), recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(!_bracelet.TryUse(_player, primaryButton: false), "Dimitri water-return fixture could not lift him.");
        for (int update = 0; update < 40; update++)
        {
            _bracelet.Update(_player, Vector2.Zero, false, true, false);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(!dimitri.TryUseBracelet(_player, Vector2I.Right), "Dimitri water-return fixture could not throw him.");
        for (int update = 0; update < 100 && dimitri.Phase == DimitriPhase.Thrown; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.ReturningToLand || dimitri.Direction != 3 || !dimitri.InWater,
            "Thrown Dimitri did not enter state $0b facing the saved throw origin.");
        for (int update = 0; update < 180 && dimitri.Phase == DimitriPhase.ReturningToLand; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(dimitri.Phase != DimitriPhase.Waiting || dimitri.InWater || dimitri.ZFixed != 0,
            "Dimitri did not swim back to land and become mountable after the throw.");
        _bracelet.Interrupt(_player, discard: true);
        GD.Print("Validated thrown Dimitri water detection, cardinal return angle and autonomous return to land.");
    }
}
