using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeedShooterReservedAngleRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int used in new[] { 4, 5 })
        foreach (int angle in new[] { 0x00, 0x04, 0x10, 0x1c })
        {
            // Approach, lift and throw the real fixture pot through the
            // existing independent Bracelet ROM/application comparison.
            var original = PrepareBraceletRom(1);
            StepBraceletRom(original, 1, batched, held: true, pressed: true);
            StepBraceletRom(original, 11, batched, 24, held: true);
            StepBraceletRom(original, 20, batched);
            StepBraceletRom(original, 1, batched, 8, held: true, pressed: true);
            StepBraceletRom(original, 9, batched);
            FailIf(_bracelet.State != BraceletState.Projectile || _bracelet.LiftedObject is null || !original.ChildActive,
                "Shooter alias fixture must retain a flying ITEM$16 in $dc after its parent has ended.");
            _inventory.GiveTreasure(TreasureId.Shooter, 1); _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x20);
            _inventory.SelectShooterSeeds(0); _inventory.SetScriptedEquippedItems(equippedB: 0, equippedA: TreasureId.Shooter);
            for (int index = 0; index < used; index++)
                _entities.Spawn<BombEffect>(new BombSpawn(_player, new BombDatabase().Data, 4, _ => { }));
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, (int)_player.Position.X, (int)_player.Position.Y);
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            // Transfer already-executed original Link/ITEM C bytes, rather
            // than synthesizing the thrown-pot state in another frontend.
            for (int index = 0; index < 0x40; index++)
            {
                native[0xd000 + index] = original[0xd000 + index];
                native[0xdc00 + index] = original[0xdc00 + index];
            }
            foreach (int address in new[] { 0xcc4f, 0xcc5a, 0xcc5b, 0xcc5c, 0xcc5f, 0xcc60 }) native[address] = original[address];
            for (int index = 0; index < used; index++)
            {
                int at = 0xd700 + index * 256; native[at] = 1; native[at + 1] = 3;
                native[at + 11] = (byte)_player.Position.Y; native[at + 13] = (byte)_player.Position.X;
            }
            Vector2 input = angle switch { 0 => Vector2.Up, 4 => new(1, -1), 0x10 => Vector2.Down, _ => new(-1, -1) };
            int held = angle switch { 0 => 0x41, 4 => 0x51, 0x10 => 0x81, _ => 0x61 };
            StepGameplayUpdates(1, input, ["attack"], ["attack"], batched);
            native.UpdateGameplay(1, held, angle, _entities.FrameCounter);
            var pot = _bracelet.LiftedObject!;
            Vector2 before = pot.GroundPosition;
            FailIf(!_seedSatchel.ShooterActive || native[0xd200] == 0 || native[0xd201] != 0x0f ||
                before != new Vector2(native.Word(0xdc0c) / 256f, native.Word(0xdc0a) / 256f) || pot.Angle != 8,
                "Shooter must start normally while the already-thrown pot still travels right.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            int expectedAngle = used == 5 ? angle : 8;
            FailIf(_entities.Entities<EmberSeedEffect>().Count != (used == 5 ? 0 : 1) || pot.Angle != expectedAngle || native[0xdc09] != expectedAngle ||
                _seedSatchel.ShooterPostShotCounter != 12 || _saveData.ReadWramByte(AgesWramAddress.wNumEmberSeeds) != 0x19 || native[AgesWramAddress.wNumEmberSeeds] != 0x19,
                $"Full Shooter allocation must still write ITEM$dc angle=${angle:x2}, consume BCD ammo and wait 12; used={used}.");
            void ComparePosition()
            {
                FailIf(_bracelet.LiftedObject is null || native[0xdc00] == 0 ||
                    pot.GroundPosition != new Vector2(native.Word(0xdc0c) / 256f, native.Word(0xdc0a) / 256f),
                    $"Shooter angle=${angle:x2}, used={used}: pot={pot.GroundPosition}, native=({native.Word(0xdc0c) / 256f},{native.Word(0xdc0a) / 256f}); failed-pointer write must reach this update's physical motion.");
            }
            ComparePosition();
            int elapsed = 0;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                elapsed++; native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); ComparePosition();
                FailIf(_seedSatchel.ShooterPostShotCounter != 12 - elapsed, "Shooter alias must preserve each post-shot counter boundary.");
            });
            _bracelet.Interrupt(_player, discard: true); LoadValidationRoom(4, 0xa1);
            FailIf(_bracelet.LiftedObject is not null, "Room cancellation must retire the redirected reserved pot.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared failed Shooter ITEM$dc angle writes and live cardinal/diagonal pot motion with original item code, plus one-free controls and cancellation.");
    }
}
