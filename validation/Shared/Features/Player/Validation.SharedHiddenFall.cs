
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedHiddenFall(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms; var memory = rooms.RuntimeState;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player; var inventory = player.Inventory;
        var app = new ApplicationValidationFixture(root); var game = memory.Game;
        foreach (int handoff in new[] { 0, 8, 4, 0x0b, 2 })
        {
            entities.Clear(); player.ClearNativeItemParents();
            player.WarpTo(new(88.25f, 64.5f)); player.Face(Vector2I.Down);
            player.SetLocalRespawnPosition(new(40, 40), Vector2I.Down);
            inventory.SetScriptedHealth(inventory.MaxHealthQuarters); session.StatusBar.SynchronizeHealth();
            memory.SetField("wDisabledObjects", 0); memory.SetField("wScrollMode", 1);
            memory.SetField("wPaletteThread_mode", 0); memory.SetField("wLinkForceState", 0);
            var native = new BraceletRom(rooms.CurrentRoom, player.PrecisePosition, 2, 0xff, game: game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xcc50] = memory.ReadField("wcc50"); native[0xcc52] = memory.ReadField("wcc52");
            native[0xd01a] = 0xc1; native[0xcc21] = 40; native[0xcc22] = 40; native[0xcc23] = 2;
            var seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            var sounds = root._sound.AttachPlayRequestAudit(); int updates = 0;
            void Set(string field, int address, byte value) { memory.SetField(field, value); native[address] = value; }
            void Step(int count = 1) => app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateLinkItems(entities.FrameCounter, 0);
                string context = $"{game}/hidden-fall->{handoff:x2}/batch{batched}/update{++updates}";
                FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                    player.Visible != ((native[0xd01a] & 0x80) != 0) ||
                    player.NativeNormalStateForInteraction != (native[0xd004] == 1),
                    context + $": position/visibility/state runtime{player.PrecisePosition}/{player.Visible}/{player.NativeNormalStateForInteraction}, native{new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f)}/${native[0xd01a]:x2}/${native[0xd004]:x2}:${native[0xd005]:x2}.");
                foreach (int address in new[] { 0xd005, 0xd006, 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        context + $": native byte${address:x4} differs: ${memory.ReadWramByte(address):x2}/${native[address]:x2}.");
                foreach (var (field, address) in new[] { ("wLinkForceState", 0xcc4f), ("wLinkStateParameter", 0xcc51), ("wcc50", 0xcc50) })
                    FailIf(memory.ReadField(field) != native[address], context + $": shared {field} differs.");
                foreach (string field in new[] { "wGameKeysPressed", "wForceLinkPushAnimation" })
                    FailIf(memory.ReadField(field) != native.ReadNativeByte(memory.FieldAddress(field)),
                        context + $": shared {field} publication differs.");
                FailIf(player.HealthQuarters != native[game == OracleGame.Ages ? 0xc6aa : 0xc6a2],
                    context + ": hidden falling must preserve the source health/damage boundary.");
                FailIf(player.PatchCollisionsEnabled != ((native[0xd024] & 0x80) != 0),
                    context + ": native Link collision-flag lifetime differs.");
                var rng = entities.Random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls - seed.Calls != native.RandomCalls ||
                    !sounds.Requests.SequenceEqual(native.Sounds), context + ": native RNG/cues differ.");
            });
            player.RequestForcedRespawn(); native[0xcc4f] = 2;
            Set("wLinkStateParameter", 0xcc51, 0); Step();
            Set("wLinkStateParameter", 0xcc51, 3); Step();
            FailIf(native[0xd005] != 4 || player.PrecisePosition != new Vector2(88.25f, 64.5f) || !player.Visible || player.PatchCollisionsEnabled,
                "State02 parameter3 must select substate4, preserve coordinate lows and disable collisions without hiding yet.");
            // The active substate does not re-read the parameter union.
            Set("wLinkStateParameter", 0xcc51, 2); Step(5);
            object owner = new();
            switch (handoff)
            {
                case 8:
                    player.RequestNativeState08Control(owner, 0xff); native.RequestState08(); native[0xcc50] = 0xff; break;
                case 4:
                    player.RequestGetItemState(0x80); native.RequestGetItemState(0x80); break;
                case 0x0b:
                    player.RequestNativeForcedMovement(4, 8, 1, owner);
                    native[0xcc4f] = 0x0b; native[0xcc51] = 4; native[0xd008] = 1; native[0xd009] = 8; break;
                case 2:
                    player.RequestForcedRespawn(); native[0xcc4f] = 2; native[0xcc51] = 2; break;
            }
            Set("wDisabledObjects", 0xcc8a, 0x81);
            if (handoff == 0)
            {
                // A raw zero counter wraps; a private36-update timer would hide too early.
                memory.SetWramByte(0xd020, 0); native[0xd020] = 0; Step(40);
                FailIf(!player.Visible || native[0xd020] != 0xd8,
                    "Hidden-fall dispatch must consume the live wrapping animation counter.");
                // A raw marker ends animation on the next visit, without advancing the cursor.
                memory.SetWramByte(0xd021, 0x80); native[0xd021] = 0x80; Step(4);
                FailIf(player.Visible || native[0xd004] != 2 || native[0xd005] != 4,
                    "Completed parameter3 must remain hidden without respawn/damage while no request exists.");
                player.RequestNativeState08Control(owner, 0xff); native.RequestState08(); native[0xcc50] = 0xff;
                Step(2); Set("wDisabledObjects", 0xcc8a, 0); Step();
            }
            else
            {
                for (int i = 0; native[0xd004] == 2 && native[0xd005] == 4 && i < 48; i++) Step();
                FailIf(native[0xcc4f] != 0 || player.Visible,
                    "The terminal hidden-fall visit must hide then consume its retained request without dispatching the destination yet.");
                Step(2);
                Set("wDisabledObjects", 0xcc8a, 0); Step(24);
            }
            FailIf(native[0xd004] != 1 || !player.NativeNormalStateForInteraction,
                "Each supported hidden-fall handoff must retire its state02 owner and complete the selected destination.");
        }
    }
}
