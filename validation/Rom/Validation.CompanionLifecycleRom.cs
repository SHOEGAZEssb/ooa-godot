using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionUpdateGatesRom()
    {
        int hostCase3 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int gate in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(id, 1);
            var restriction = new CompanionUpdateGate();
            _entities.AddEntity(restriction);
            var palette = _entities.PaletteFadeActiveSource;
            var disabled = _entities.InitializedObjectsDisabledSource;
            try
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    StepCompanionRom(actor, rom, 4, Vector2.Right, batched);
                    restriction.Active = gate == 0; rom[0xcc8a] = gate == 0 ? (byte)0x20 : gate == 2 ? (byte)0x80 : (byte)0;
                    _entities.PaletteFadeActiveSource = () => gate == 1;
                    _entities.InitializedObjectsDisabledSource = () => gate == 2;
                    rom[0xc4ab] = gate == 1 ? (byte)1 : (byte)0;
                    StepCompanionRom(actor, rom, 5, Vector2.Left, batched);
                    restriction.Active = false; rom[0xcc8a] = rom[0xc4ab] = 0;
                    _entities.PaletteFadeActiveSource = palette;
                    _entities.InitializedObjectsDisabledSource = disabled;
                    StepCompanionRom(actor, rom, 4, Vector2.Left, batched);
                }
            }
            finally { _entities.PaletteFadeActiveSource = palette; _entities.InitializedObjectsDisabledSource = disabled; }
        }
    }

    private void ValidateDimitriWaterDismountRom()
    {
        int hostCase2 = 0;
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0c, direction);
            SetCompanionRomTile(rom, 4, 4, 0xfe);
            StepCompanionRom(actor, rom, 3, Vector2.Zero, batched);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int frame = 0;
                StepGameplayUpdates(3, Vector2.Zero, ["item"], ["item"], batched, () =>
                {
                    rom.Update(0xff, frame++ == 0 ? (byte)2 : (byte)0, 2);
                    CompareCompanionMotion(actor, rom, "Dimitri water dismount refusal");
                    FailIf(!_player.CompanionRideActive || rom[0xcc2c] != 0xd1, "Dimitri dismounted into water.");
                });
                StepCompanionRom(actor, rom, 1, Vector2.Zero, batched);
            }
        }
    }

    private void ValidateCompanionScrollRom()
    {
        var graphics = new ScreenTransitionGraphicsDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        for (int direction = 0; direction < 4; direction++)
        {
            var (actor, _) = PrepareMountedCompanionRom(id, direction);
            StepGameplayUpdates(1, Vector2.Zero);
            var rider = (IPlayerRideableRoomEntity)actor;
            var transition = (IPlayerScreenTransitionRoomEntity)actor;
            var animation = CompanionField<EnemyAnimationPlayer>(actor, "_animation");
            int animationCounter = CompanionField<int>(animation, "_frameCounter");
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique;
            Vector2I vector = (Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8);
            _transitions.BeginScroll(_player, vector, 0x2b);
            Vector2 start = transition.ScreenTransitionPosition;
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique;
            var rom = new ScrollRom(false, direction, (int)(_player.PrecisePosition.X * 256),
                (int)(_player.PrecisePosition.Y * 256), unique, sourceUnique);
            rom[0xcc2c] = 0xd1; rom[0xd100] = 1; rom[0xd101] = (byte)id;
            rom[0xd108] = (byte)direction; rom[0xd13c] = 1;
            rom.Word(0xd10c, (int)(start.X * 256)); rom.Word(0xd10a, (int)(start.Y * 256));
            int update = 0;
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.Update(); update++;
                Vector2 actual = transition.ScreenTransitionPosition;
                int x = (int)(actual.X * 256) & 0xffff, y = (int)(actual.Y * 256) & 0xffff;
                FailIf(x != rom.Word(0xd10c) || y != rom.Word(0xd10a) || !rider.LinkRiding ||
                    _transitions.ScrollActive != (rom[0xcd04] != 2) || CompanionField<int>(animation, "_frameCounter") != animationCounter,
                    $"Companion ${id:x2} scroll direction={direction}, update={update}: native XY={rom.Word(0xd10c)}/{rom.Word(0xd10a)}, runtime={x}/{y}.");
            });
            FailIf(CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState) != new Vector2(rom[0xc639], rom[0xc638]),
                "Companion scroll did not publish the native last-mount coordinates.");
        }
    }

    private void ValidateRickyTornadoAllocationRom()
    {
        int hostCase1 = 0;
        foreach (int used in new[] { 0, 4, 5 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0b);
            var pool = CompanionField<DynamicItemSlotPool>(_entities, "_dynamicItems");
            bool occupied = true;
            for (int slot = 0; slot < used; slot++)
            {
                pool.TryAllocate(new object(), 0xff, () => occupied);
                rom[(0xd7 + slot) << 8] = 1; rom[((0xd7 + slot) << 8) + 1] = 0xff;
            }
            StepCompanionRom(actor, rom, 80, Vector2.Zero, batched, attack: true, edge: true);
            StepCompanionRom(actor, rom, 45, Vector2.Zero, batched);
            occupied = false;
            for (int slot = 0; slot < used; slot++) rom[(0xd7 + slot) << 8] = 0;
            StepCompanionRom(actor, rom, 80, Vector2.Zero, batched, attack: true, edge: true);
            StepCompanionRom(actor, rom, 45, Vector2.Zero, batched);
        }
    }
}
