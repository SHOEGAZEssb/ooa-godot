
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateScriptedCompanionScrollRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0d })
        foreach (int flags in new[] { 0, 0x20 })
        foreach (int warm in new[] { 3, 1, 2 })
        {
            if (flags != 0 && (id == 0x0b || warm == 3)) continue;
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0);
            _inventory.GiveTreasure(0x52, 0);
            _saveData.SetGlobalFlag(0x15);
            _saveData.SetRoomFlag(0, 0x6a, 0x40);
            _saveData.WriteWramByte(0xc646, 0); _saveData.WriteWramByte(0xc648, (byte)flags);
            _saveData.CommitInventoryChange();
            int source = id == 0x0b ? 0x6a : 0x6b, destination = id == 0x0b ? 0x6b : 0x6a;
            var seed = _random.CaptureState();
            LoadValidationRoom(0, source);
            Vector2 link = new(104, 104);
            _player.WarpTo(link, recordSafe: false);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 2, (int)link.X, (int)link.Y)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay(); native[0xd000] = 3; // bank1 loadGame: Link survives setObjectsEnabledTo2.
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            int updates = 0;
            void Compare()
            {
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd110, 0xd114, 0xd115,
                    0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130, 0xd13d, 0xd13e, 0xd13f })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Outgoing SPECIALOBJECT ${id:x2}, flags=${flags:x2}, warm={warm}, batch={batched}, update{updates}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                foreach (int address in new[] { 0xcc02, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28, 0xcc2c, 0xcc6e, 0xcc8a, 0xcc91 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Outgoing SPECIALOBJECT ${id:x2}: control/remembered byte${address:x4} differs during scroll.");
                FailIf(_saveData.ReadWramByte(0xc648) != native[0xc648] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "Outgoing companion initialization must preserve source quest flags and ordered room RNG.");
            }
            var app = new ApplicationValidationFixture(this);
            app.Step(warm, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
            var body = _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Single();
            OracleRoomData next = _world.LoadRoom(0, destination);
            // Supply the same room-boundary input to both callers. Scroll
            // motion has separate ROM coverage; this checks native slot life.
            Vector2I direction = id == 0x0b ? Vector2I.Right : Vector2I.Left;
            link = new(id == 0x0b ? 154 : 5, 104);
            _player.WarpTo(link, recordSafe: false);
            native.Word(0xd00a, (int)link.Y * 256); native.Word(0xd00c, (int)link.X * 256);
            native.ClearRoomVariables(scrolling: true); native.BeginObjectScroll();
            native[0xcc30] = (byte)destination; native.CopyRoom(next);
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            _transitions.BeginValidationScroll(_player, direction, destination);
            native.AdvanceInteractions(); Compare();
            FailIf(!_entities.IsOutgoingEntity(body) || body.Npc.IsRetired || !_entities.IsSpecialObjectOwner(body),
                "Destination entry cancellation must leave the outgoing native D1 allocation and presenter intact.");
            app.Step(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
            if (flags == 0x20)
            {
                var normal = _entities.OutgoingEntities<MooshCompanionRoomEntity>().Single();
                // The first three updates remain in scroll setup states3-5;
                // they do not advance the camera by any pixels.
                FailIf(!body.Npc.IsRetired || !_entities.IsSpecialObjectOwner(normal) ||
                    normal.TransitionDrawOffset != Vector2.Zero || native[0xd104] != 1,
                    "Outgoing state0 must select the destination's normal handler and transfer in place, retaining its outgoing projection and deletion lifetime.");
            }
            bool completed = false;
            for (int limit = 0; limit < 96 && !completed; limit++)
                app.Step(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                    if (!completed && !_transitions.ScrollActive)
                    {
                        native.FinishObjectScroll(); native[0xcd00] = 1; completed = true;
                    }
                    Compare();
                });
            FailIf(!completed, "The actual transition controller must finish the outgoing companion's scroll.");
            FailIf(!body.Npc.IsRetired || _runtimeState.ReadWramByte(0xd100) != 0,
                "Only scroll completion clears the unretained D1 bytes and retires its presentation.");
            app.Step(2, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
            LoadValidationRoom(0, source); app.Step(1, Vector2.Zero, batched: batched);
            FailIf(_runtimeState.ReadWramByte(0xd100) != 1 || _runtimeState.ReadWramByte(0xd101) != id ||
                _runtimeState.ReadWramByte(0xd104) != 0,
                "Re-entry must let the original placed spawner create one fresh pending D1 body after outgoing deletion.");
        }
    }
}
