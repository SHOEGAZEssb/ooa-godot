using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateForestCompanionBodiesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int room in new[] { 0x63, 0x81 })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.AssignAnimalCompanion(id);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24, room == 0x63);
            _saveData.WriteWramByte(0xc646 + id - 0x0b, 0x60); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, room);
            Vector2 link = new(120, 112); _player.WarpTo(link, recordSafe: false);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)link.X, (int)link.Y)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            int updates = 0;
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd110, 0xd114, 0xd115,
                    0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130, 0xd138, 0xd139, 0xd13d, 0xd13f })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Forest${room:x2} SPECIALOBJECT${id:x2} update{updates}, batch={batched}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc2c, 0xcc6e, 0xcc91, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Forest${room:x2} SPECIALOBJECT${id:x2}: control byte${address:x4} differs.");
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0) ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Forest${room:x2} SPECIALOBJECT${id:x2} update{updates}: text port={_dialogue.IsOpen}/ROM={native[0xcba0]:x2}, RNG port={_random.Calls}/ROM={seed.Calls + native.RandomCalls}, seeds port={_random.CaptureState().Rng1:x2}/{_random.CaptureState().Rng2:x2}, ROM={native[0xff94]:x2}/{native[0xff95]:x2}.");
            }
            new ApplicationValidationFixture(this).Step(room == 0x63 ? 7 : 24, Vector2.Zero, batched: batched, afterUpdate: Compare);
            var body = _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Single();
            FailIf(body.NativeSpecialObjectId != id || !body.Npc.Visible || _runtimeState.ReadWramByte(0xd104) != 0x0a,
                "The forest producer must bind the selected native scripted body after its original state0 visits.");
        }
    }
}
