using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDiveOntoZoraRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(1, 0xb1);
            // Native sea geometry, above the destination $ab:$18 at $3848.
            // The foot tile, not the exact surface XY, sets the dive anchor.
            _player.WarpTo(new(0x48, 0x32)); _player.Face(Vector2I.Up);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x48, 0x32);
            rom[0xd009] = 0xff; // Direct-load fixture starts with no retained movement input.
            rom[0xcc05] = 1; // initializeRoom admits its placed object stream.
            rom.InitializeLinkGameplay();
            int previousHeld = 0, update = 0;
            bool loaded = false;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previousHeld; previousHeld = held;
                Vector2 movement = (held & 0x20) != 0 ? Vector2.Left : Vector2.Zero;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.AdvanceWarpPalette();
                    if (rom[0xc2ef] == 3)
                    {
                        rom.UpdateFadeOutWarp();
                        loaded = rom[0xcc2d] == 3;
                    }
                    else
                    {
                        rom.UpdateGameplay(edge, held, movement == Vector2.Left ? 0x18 : 0xff, _entities.FrameCounter);
                        if (!loaded && rom[0xcc4b] != 0) rom.ApplyRequestedWarp();
                    }
                    edge = 0;
                    FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _currentRoom.Id != rom[0xcc30] ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        $"Dive onto room3:b1 $ab:$18, batch={batched}, update={++update}: " +
                        $"Link XY port={_player.PrecisePosition}, ROM=${rom.Word(0xd00c):x4}/${rom.Word(0xd00a):x4}, " +
                        $"room={_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}, native=${rom[0xcc2d]:x}:${rom[0xcc30]:x2}, " +
                        $"slot=${rom[0xd241]:x2}:${rom[0xd242]:x2}, state=${rom[0xd244]:x2}, " +
                        $"slot XY=${rom[0xd24d]:x2}/${rom[0xd24b]:x2}, radii=${rom[0xd266]:x2}/${rom[0xd267]:x2}, " +
                        $"Link radii=${rom[0xd026]:x2}/${rom[0xd027]:x2}, pass=${rom[0xcc8c]:x2}, palette=${rom[0xc4ab]:x2}, " +
                        "slots=" + string.Join(",", Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
                            .Where(slot => rom[slot] != 0).Select(slot => $"{slot:x4}:{rom[slot + 1]:x2}:{rom[slot + 2]:x2}/{rom[slot + 4]:x2}/{rom[slot + 0x26]:x2}")));
                });
            }
            Step(60);
            FailIf(!_player.TopDownSwimming || _collision.Collides(_player.Position),
                "Room1:b1 dive approach must establish swimming through its actual sea geometry.");
            Step(1, 2); Step(1, 2);
            FailIf(!_transitions.IsTransitioning || _transitions.ActiveWarpDestinationPosition != 0x34,
                "Dive over room3:b1 Zora must capture tile $34 without rejecting or relocating its destination.");
            Step(32);
            FailIf(!loaded || _player.Position != new Vector2(0x48, 0x38),
                "Deep-water reload must first place Link at the source tile center, overlapping $ab:$18.");
            Step(1);
            FailIf(_player.Position != new Vector2(0x48, 0x38),
                "Zora state0 must initialize without separating Link on that update.");
            Step(1);
            FailIf(_player.Position != new Vector2(0x3c, 0x38) || _collision.Collides(_player.Position) ||
                _entities.BlocksLink(_player.Position),
                "Zora state1 must separate a centered overlap horizontally left by $0c, including during arrival fade.");
            Step(31);
            FailIf(_transitions.IsTransitioning, "Dive arrival did not release normal gameplay.");
            Vector2 escaped = _player.Position;
            Step(1, 0x20); Step(8, 0x20); Step(20);
            FailIf(_player.Position.X >= escaped.X || _collision.Collides(_player.Position) ||
                _entities.BlocksLink(_player.Position), "Link must be able to swim away after the Zora separates him.");
            // Approach again through the actual floor; the resolver must not
            // lose solidity or leave a stale collision after its arrival push.
            for (int i = 0; i < 80 && _player.Position.X < 0x3c; i++)
                StepGameplayUpdates(1, Vector2.Right, ["move_right"], i == 0 ? ["move_right"] : [], batched);
            var zora = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xab);
            _player.Face(Vector2I.Right);
            FailIf(!zora.CanTalkTo(_player) || _entities.BlocksLink(_player.Position),
                "Room3:b1 Zora must remain reachable and solid after dive overlap recovery.");

            // Return through real B-button surfacing and repeat the dive in
            // the same gameplay session, retaining room/actor lifecycle state.
            void RuntimeStep(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
            for (int i = 0; i < 120 && _transitions.IsTransitioning; i++) RuntimeStep(1);
            FailIf(_rooms.ActiveGroup != 1 || _currentRoom.Id != 0xb1 || _transitions.IsTransitioning,
                "Room3:b1 tile $33 must permit ordinary surfacing after recovering from the Zora overlap.");
            RuntimeStep(60);
            for (int i = 0; i < 80 && _player.PrecisePosition.X < 65; i++)
            {
                RuntimeStep(1);
                StepGameplayUpdates(6, Vector2.Right, ["move_right"], ["move_right"], batched);
            }
            RuntimeStep(60);
            StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
            RuntimeStep(1);
            FailIf(!_transitions.IsTransitioning || _transitions.ActiveWarpDestinationPosition != 0x34,
                "Repeated dive must remain reachable through room1:b1 sea geometry and select tile $34.");
            RuntimeStep(32);
            FailIf(_rooms.ActiveGroup != 3 || _player.Position != new Vector2(0x48, 0x38),
                "Repeated dive must retain the original overlapping destination anchor.");
            RuntimeStep(2);
            FailIf(_player.Position != new Vector2(0x3c, 0x38) || _entities.BlocksLink(_player.Position),
                "Repeated dive must recreate the Zora's native initialization and overlap separation.");
            RuntimeStep(31);
            FailIf(_transitions.IsTransitioning || _collision.Collides(_player.Position),
                "Repeated dive recovery must release Link on walkable ocean floor.");
        }
        GD.Print("Validated clean-US room1:b1 dive onto room3:b1 Zora: native initialization, arrival-fade separation, escape and repeated approach in split/batched gameplay.");
    }
}
