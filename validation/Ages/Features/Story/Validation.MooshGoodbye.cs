using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom06bMooshGoodbye()
    {
        var database = new MooshGoodbyeEventDatabase();
        MooshGoodbyeEventRecord record = database.Record;
        void StepRoomEventFrames(int frames) => StepGameplayUpdates(frames, Vector2.Zero);
        int Word(int address) => unchecked((short)(_runtimeState.ReadWramByte(address) | _runtimeState.ReadWramByte(address + 1) << 8));

        void Configure(int mooshState, bool hasRope)
        {
            CompanionRuntimeState.Clear(
                _runtimeState, CompanionRuntimeState.MooshId);
            CompanionRuntimeState.ForgetRemembered(_runtimeState);
            _inventory.LoseTreasure(record.TreasureId);
            if (hasRope)
                _inventory.GiveTreasure(record.TreasureId, 0);
            _saveData.WriteWramByte(record.MooshStateAddress, (byte)mooshState);
            _saveData.CommitInventoryChange();
        }

        Configure(record.RescuedMask, hasRope: false);
        LoadValidationRoom(record.Group, record.Room);
        StepRoomEventFrames(2);
        FailIf(
            _entities.Entities<MooshCompanionRoomEntity>().Any() ||
            _dialogue.IsOpen,
            "Room 0:6b spawned INTERAC_COMPANION_SPAWNER $67:$01 " +
            "without the Cheval Rope treasure `$52.");

        Configure(record.RescuedMask | record.LeftMask, hasRope: true);
        LoadValidationRoom(record.Group, record.Room);
        StepRoomEventFrames(2);
        FailIf(
            _entities.Entities<MooshCompanionRoomEntity>().Any() ||
            _dialogue.IsOpen,
            "wMooshState bit `$40 did not suppress room 0:6b's $67:$01 " +
            "Moosh goodbye spawner.");

        Configure(record.RescuedMask, hasRope: true);
        CompanionRuntimeState.Remember(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            group: 1,
            room: 0x23,
            position: new Vector2(0x45, 0x67));
        LoadValidationRoom(record.Group, record.Room);

        FailIf(_entities.EntityAdapters<ScriptedCompanionRoomEntity>().Any() || _runtimeState.ReadWramByte(0xd100) != 0,
            "The placed $67:$01 must wait for its first native interaction visit before creating D1.");
        StepRoomEventFrames(1);
        AgesMooshScriptedCompanionRoomEntity moosh =
            _entities.EntityAdapters<AgesMooshScriptedCompanionRoomEntity>().Single();
        RememberedCompanion remembered =
            CompanionRuntimeState.ReadRemembered(_runtimeState);
        FailIf(
            moosh.NativeSpecialObjectPosition != new Vector2(record.MooshX, record.MooshY) ||
            moosh.RoomInitialization?.Pending != true ||
            _runtimeState.ReadWramByte(0xd104) != 0 ||
            remembered.Id != 0 || remembered.Group != 1 ||
            remembered.Room != 0x23 || remembered.Y != 0x67 ||
            remembered.X != 0x45 ||
            _dialogue.IsOpen,
            "Room 0:6b did not preserve $71:$01/$67:$01 source order, " +
            "Moosh's `$48,$38 preset, or the spawner's ID-only remembered " +
            "companion clear.");

        StepRoomEventFrames(1);
        FailIf(moosh.RoomInitialization?.Pending != true ||
            _runtimeState.ReadWramByte(0xd104) != 0 || _runtimeState.ReadWramByte(0xd105) != 1 ||
            _runtimeState.ReadWramByte(0xd100) != 1 || _runtimeState.ReadWramByte(0xd101) != 0x0d ||
            moosh.Npc.Visible || _runtimeState.ReadField("wMenuDisabled") != 0,
            "Ages Moosh birth must retain the source state0/substate1 yield before the farewell initializer.");
        StepRoomEventFrames(1);
        FailIf(
            moosh.RoomInitialization?.Pending != false ||
            _runtimeState.ReadWramByte(0xd104) != 0x0a || _runtimeState.ReadWramByte(0xd103) != 4 ||
            _runtimeState.ReadField("wDisabledObjects") != 1 || _runtimeState.ReadField("wMenuDisabled") != 1 ||
            _dialogue.IsOpen,
            "SPECIALOBJECT_MOOSH state `$0a did not disable objects/menu and " +
            "select animation `$01 on its source initialization update.");

        int mooshSoundRequests = _sound.PlayRequestsFor(SoundId.SndMoosh);
        StepRoomEventFrames(1);
        FailIf(
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Text) ||
            _runtimeState.ReadWramByte(0xd103) != 5,
            "Room 0:6b did not show imported TX_2208 on the update after " +
            "Moosh's state-$0a initializer.");
        // initialFileVariables selects speed $02; textSpeedData waits four updates.
        _dialogue.AdvanceNeutralCharacterClockForValidation(4.0 / 60.0);
        FailIf(
            _sound.PlayRequestsFor(SoundId.SndMoosh) != mooshSoundRequests + 1,
            "TX_2208 did not execute its leading source `\\sfx(0xc5)` cue.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            _runtimeState.ReadWramByte(0xd103) != 6 ||
            Word(0xd114) != record.InitialSpeedZ ||
            Word(0xd10e) != 0 ||
            _runtimeState.ReadWramByte(0xd130) != record.FlightAnimation ||
            moosh.NativeSpecialObjectPosition != new Vector2(record.MooshX, record.MooshY),
            "Closing TX_2208 did not initialize speedZ=-$0140, angle `$10, " +
            "SPEED_100, and animation `$0b without moving on that update.");

        int expectedZ = 0;
        int expectedSpeedZ = record.InitialSpeedZ;
        const int verticalFlightUpdates = 20;
        for (int update = 0; update < verticalFlightUpdates; update++)
        {
            OracleObjectMath.UpdateSpeedZ(
                ref expectedZ, ref expectedSpeedZ, record.FlightGravity);
        }
        StepRoomEventFrames(verticalFlightUpdates);
        FailIf(
            Word(0xd10e) != expectedZ ||
            Word(0xd114) != expectedSpeedZ ||
            moosh.NativeSpecialObjectPosition != new Vector2(record.MooshX, record.MooshY) ||
            expectedSpeedZ != 0,
            "Moosh's farewell launch did not perform exactly 20 `$10-gravity " +
            "updates before the speedZ high byte became zero.");

        OracleObjectPosition expectedPosition =
            OracleObjectMovement.Shared.PositionFromPixels(moosh.NativeSpecialObjectPosition);
        int downwardFlightUpdates = record.ExitY - record.MooshY;
        for (int update = 0; update < downwardFlightUpdates - 1; update++)
        {
            expectedPosition = OracleObjectMovement.Shared.ApplySpeed(
                expectedPosition, record.FlightSpeed, record.FlightAngle);
        }
        StepRoomEventFrames(downwardFlightUpdates - 1);
        FailIf(
            moosh.Finished ||
            moosh.NativeSpecialObjectPosition != expectedPosition.PixelPosition ||
            Mathf.FloorToInt(moosh.NativeSpecialObjectPosition.Y) != record.ExitY - 1 ||
            (_saveData.ReadWramByte(record.MooshStateAddress) &
                record.LeftMask) != 0 ||
            _runtimeState.ReadField("wMenuDisabled") != 1,
            "Moosh left room 0:6b before the source Y=$f0 boundary or " +
            "released input during his SPEED_100 flight.");

        StepRoomEventFrames(1);
        remembered = CompanionRuntimeState.ReadRemembered(_runtimeState);
        FailIf(
            !moosh.Finished || moosh.Npc.Visible ||
            _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Any() ||
            (_saveData.ReadWramByte(record.MooshStateAddress) &
                record.LeftMask) == 0 ||
            remembered.Id != 0 || remembered.Group != 1 ||
            remembered.Room != 0x23 || remembered.Y != 0x67 ||
            remembered.X != 0x45 ||
            _entities.PlayerSwordDisabled ||
            _entities.PlayerItemUsageDisabled ||
            _entities.PlayerMovementDisabled ||
            _entities.PlayerMenusDisabled,
            "Moosh's Y=$f0 update did not set wMooshState bit `$40, clear " +
            "only wRememberedCompanionId, delete the actor, and release input.");

        LoadValidationRoom(record.Group, record.Room);
        StepRoomEventFrames(2);
        FailIf(
            _entities.Entities<MooshCompanionRoomEntity>().Any() ||
            _dialogue.IsOpen,
            "Room 0:6b replayed TX_2208 after persistent wMooshState bit `$40.");

        GD.Print(
            "Validated room 0:6b $71:$01/$67:$01 Moosh goodbye: Cheval " +
            "Rope/left-bit predicates, fixed preset, TX_2208/SND_MOOSH, exact " +
            "state-$0a input timing, -$0140 Z launch, SPEED_100 Y=$f0 exit, " +
            "ID-only remembered clear, persistent bit `$40, and re-entry suppression.");
    }
}
