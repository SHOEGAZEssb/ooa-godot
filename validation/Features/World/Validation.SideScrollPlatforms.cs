using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMovingSideScrollPlatforms()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var placements = new MovingSideScrollPlatformDatabase();
        var interactions = new DungeonInteractionDatabase();
        var visuals = new DungeonInteractionVisualDatabase();

        foreach ((int subId, Vector2I size, Vector2 offset) in new[]
        {
            (0x02, new Vector2I(32, 16), new Vector2(-16, -8)),
            (0x0a, new Vector2I(48, 16), new Vector2(-24, -8)),
            (0x04, new Vector2I(16, 48), new Vector2(-8, -24)),
            (0x0b, new Vector2I(32, 48), new Vector2(-16, -24)),
            (0x00, new Vector2I(16, 16), new Vector2(-8, -8))
        })
        {
            var preview = new MovingSideScrollPlatformRoomEntity(
                new MovingSideScrollPlatformPlacement(
                    5, 0x06, 0, 0xa1, subId, 0, 0, "validation"),
                interactions.SidePlatform(subId),
                visuals.Visual("moving-side-platform"));
            FailIf(
                preview.CurrentTexture.GetSize() != size ||
                preview.CurrentDrawOffset != offset,
                $"INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:${subId:x2} " +
                $"clipped its positioned OAM frame: expected {size} at " +
                $"{offset}, got {preview.CurrentTexture.GetSize()} at " +
                $"{preview.CurrentDrawOffset}.");
            preview.Free();
        }

        FailIf(
            placements.GetRoomRecords(5, 0x06) is not
                [{ Order: 0, Id: 0xa1, SubId: 0x0b, Y: 0x68, X: 0x68 }],
            "Room 7:06 did not retain group5Map06ObjectData's source " +
            "INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:$0b placement.");
        MovingSideScrollPlatformRecord script = interactions.SidePlatform(0x0b);
        FailIf(
            script is not { Speed: 0x14, Direction: 3, RadiusY: 0x19, RadiusX: 0x0f } ||
            script.Commands is not
                [
                    { Direction: MovingSideScrollPlatformDirection.Left, Endpoint: 0x40 },
                    { Direction: MovingSideScrollPlatformDirection.Wait, Endpoint: 30 },
                    { Direction: MovingSideScrollPlatformDirection.Right, Endpoint: 0x80 },
                    { Direction: MovingSideScrollPlatformDirection.Wait, Endpoint: 30 }
                ],
            "INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:$0b did not retain its " +
            "SPEED_80, direction-$03 collision shape, or movement script.");

        LoadValidationRoom(7, 0x06);
        _player.WarpTo(new Vector2(0x18, 0x18), recordSafe: false);
        MovingSideScrollPlatformRoomEntity platform =
            _entities.Entities<MovingSideScrollPlatformRoomEntity>().Single();
        FailIf(
            platform.Position != new Vector2(0x68, 0x68) ||
            platform.CurrentAnimationIndex != 3 ||
            platform.CurrentTexture.GetWidth() != 32 ||
            platform.CurrentTexture.GetHeight() != 48 ||
            platform.CurrentDrawOffset != new Vector2(-16, -24) ||
            platform.Position.Y + platform.CurrentDrawOffset.Y !=
                platform.Position.Y - script.RadiusY + 1 ||
            platform.Position.Y + platform.CurrentDrawOffset.Y +
                platform.CurrentTexture.GetHeight() !=
                platform.Position.Y + script.RadiusY - 1,
            "Room 7:06 did not create its direction-$03 moving platform at " +
            "source position ($68,$68) with the full 32x48 positioned OAM " +
            "frame aligned to collision radius Y=$19.");

        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(update, _player);
        }

        // objectLoadMovementScript selects the first command on state zero;
        // objectApplySpeed begins on the following original update.
        Step();
        FailIf(
            platform.Position != new Vector2(0x68, 0x68) ||
            platform.CommandIndex != 0,
            "Room 7:06's $a1:$0b platform moved on its initialization update.");

        Step(79);
        FailIf(
            platform.Position != new Vector2(0x40, 0x68) ||
            platform.CommandIndex != 0,
            "Room 7:06's $a1:$0b platform did not reach X=$40 with its " +
            "source 8.8 fractional byte intact.");
        Step();
        FailIf(
            platform.CommandIndex != 1 ||
            platform.WaitCounter != 30 ||
            platform.Position != new Vector2(0x40, 0x68),
            "Room 7:06's $a1:$0b platform did not enter ms_wait 30 on the " +
            "endpoint update.");

        Step(29);
        FailIf(
            platform.CommandIndex != 1 ||
            platform.WaitCounter != 1 ||
            platform.Position != new Vector2(0x40, 0x68),
            "Room 7:06's $a1:$0b platform did not hold X=$40 through wait " +
            "counter $01.");
        Step();
        FailIf(
            platform.CommandIndex != 2 ||
            platform.Position != new Vector2(0x40, 0x68),
            "Room 7:06's $a1:$0b platform moved on the wait-counter zero update.");
        Step();
        FailIf(
            platform.Position != new Vector2(0x41, 0x68),
            "Room 7:06's $a1:$0b platform did not resume rightward movement " +
            "on the update after wait-counter zero.");
    }
}
