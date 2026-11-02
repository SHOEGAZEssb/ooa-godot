
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static NativeObjectInstruction[] SideViewObjectInstructions(int group, int room)
    {
        var data = NativeObjectStreamDatabase.For(AgesGameContent.AssetRoot);
        var memory = OracleRuntimeState.CreateInitialized(OracleGame.Ages);
        data.InitializeLoadFlags(memory);
        return new NativeObjectStreamReader(data, memory, () => 0).Read(data.Root(group, room)).ToArray();
    }

    private void ValidateMovingSideScrollPlatforms()
    {
        var interactions = new DungeonInteractionDatabase(AgesGameContent.AssetRoot);
        var visuals = new DungeonInteractionVisualDatabase(AgesGameContent.AssetRoot);

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
                Vector2.Zero,
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
            SideViewObjectInstructions(5, 0x06) is not
                [{ Opcode: 2, Id: 0xa1, SubId: 0x0b, Y: 0x68, X: 0x68 }],
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

        ReinitializeGameplayForValidation();
    }

    private void ValidateNativePlatformPlacement()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(6, 0x29);
            _entities.Clear();
            for (int slot = 2; slot < 16 - free; slot++)
                _entities.Spawn<StationaryDebrisEffect>(new PuzzlePuffSpawn(new(200, 120), SoundId.MusNone));
            _runtimeState.SetField("wcc05", 0xff);
            _runtimeState.SetField("wcc85", 1); // Suppress the enemy pointer, retaining main interactions.
            var random = _random.CaptureState();
            var factory = SomariaPrivate<AgesRoomEntityFactory>(_entities, "_factory");
            foreach (var entity in factory.CreateRoomEntities(6, _currentRoom, EnemyPlacementContext.Unrestricted))
                _entities.AddEntity(entity);
            var platforms = _entities.Entities<MovingSideScrollPlatformRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
            var after = _random.CaptureState();
            FailIf(platforms.Length != System.Math.Min(free, 2) || after.Calls != random.Calls ||
                after.Rng1 != random.Rng1 || after.Rng2 != random.Rng2 || after.LastResult != random.LastResult ||
                after.PlacementIndex != random.PlacementIndex || after.PlacementBufferReady != random.PlacementBufferReady ||
                !after.PlacementBuffer.SequenceEqual(random.PlacementBuffer) ||
                _runtimeState.ReadField("wcc85") != 1,
                $"INTERAC$a1 native construction/free{free}: allocation-run admission, RNG or pointer gate changed.");
            for (int i = 0; i < platforms.Length; i++)
                FailIf(_entities.InteractionSlot(platforms[i]) != 16 - free + i ||
                    platforms[i].Position != (i == 0 ? new Vector2(0x58, 0x58) : new Vector2(0x98, 0x68)),
                    $"INTERAC$a1 native construction/free{free}: compressed object run changed source order or position.");
            _player.WarpTo(new(24, 56));
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(platforms.Any(platform => platform.UpdatesDuringDialogue),
                "Native platform construction must retain ordinary state-zero initialization in the gameplay loop.");
        }
        ReinitializeGameplayForValidation();
    }
}
