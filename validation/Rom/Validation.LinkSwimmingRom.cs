using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkSwimmingRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            rom[0xc69f] = 0x40; // TREASURE_FLIPPERS $2e
            rom[0xcc33] = (byte)room.ActiveCollisions;
            rom[0xd000] = 1;
            rom[0xd004] = 1;
            rom[0xd01a] = 0x80;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xfa, 0x10, 0);
                rom[0xcf00 + y * 16 + x] = 0xfa;
                rom[0xce00 + y * 16 + x] = 0x10;
            }
            _player.WarpTo(new(80, 64));
            rom.Word(0xd00a, 64 * 256);
            rom.Word(0xd00c, 80 * 256);
            rom[0xd009] = 0xff;
            int update = 0;
            int Field(string name) => (int)typeof(Player).GetField(name, flags)!.GetValue(_player)!;
            void Compare()
            {
                rom.Call(LinkCollisionRom.ApplyTile);
                rom.Call(LinkCollisionRom.Probe);
                rom.Call(LinkCollisionRom.Swim);
                var expected = new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected || _player.TopDownSwimmingState != (rom[0xcc5d] & 15) ||
                    _player.TopDownDiving != ((rom[0xcc5d] & 0x80) != 0) ||
                    Field("_topDownSwimSpeedRaw") != rom[0xd010] || Field("_topDownSwimAngle") != rom[0xd009],
                    $"ROM swimming update={update}, batch={batched}: ROM XY={expected}, state=${rom[0xcc5d]:x2}, speed/angle=${rom[0xd010]:x2}/${rom[0xd009]:x2}; runtime XY={_player.PrecisePosition}, state={_player.TopDownSwimmingState}, diving={_player.TopDownDiving}, speed/angle=${Field("_topDownSwimSpeedRaw"):x2}/${Field("_topDownSwimAngle"):x2}.");
                void Check(int actual, int address, string label) => FailIf(actual != rom[address],
                    $"ROM swimming {label}, update={update}, batch={batched}: ROM=${rom[address]:x2}, runtime=${actual:x2}.");
                Check(_player.TopDownSwimTargetSpeedRaw, 0xd011, "target speed");
                Check(Field("_topDownSwimVelocityCounter"), 0xd012, "velocity counter");
                Check(_player.TopDownSwimBurstState, 0xd035, "burst state");
                Check(_player.TopDownSwimAnimationCounter, 0xd020, "animation counter");
                if (_player.TopDownSwimmingState == 2)
                    Check(_player.TopDownSwimmingEntryCounter, 0xd006, "entry counter");
                if (_player.TopDownSwimBurstState != 0)
                    Check(_player.TopDownSwimBurstCounter, 0xd006, "burst counter");
                if (_player.TopDownDiving)
                    Check(Field("_topDownDiveCounter"), 0xd007, "dive counter");
                update++;
                rom[0xcc2a] = 0;
            }
            for (int block = 0; block < 24; block++)
            {
                int angle = block < 2 ? 0xff : ((block / 2) & 3) * 8;
                Vector2 input = angle switch { 0 => Vector2.Up, 8 => Vector2.Right, 16 => Vector2.Down, 24 => Vector2.Left, _ => Vector2.Zero };
                rom[0xcc2b] = (byte)angle;
                bool burst = block is 2 or 7 or 12;
                bool dive = block is 4 or 21 or 22;
                rom[0xcc2a] = (byte)((burst ? 1 : 0) | (dive ? 2 : 0));
                string[] pressed = burst ? ["attack"] : dive ? ["item"] : [];
                StepGameplayUpdates(8, input, held: pressed, pressed: pressed, batched: batched, afterUpdate: Compare);
            }
            // Rotate the current while keeping the same native swim state and momentum.
            foreach (byte tile in new byte[] { 0xe0, 0xe3, 0xe1, 0xe2 })
            {
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 10; x++)
                {
                    room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), tile, 0x10, 0);
                    rom[0xcf00 + y * 16 + x] = tile;
                }
                rom[0xcc2b] = 0xff;
                StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: Compare);
            }
        }
        GD.Print("Validated native swimming entry, momentum, turning, bursts, diving and four currents through individual/batched gameplay updates.");
    }
}
