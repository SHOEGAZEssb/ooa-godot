using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSpinner(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var inventory = player.Inventory;
        var game = memory.Game;
        var app = new ApplicationValidationFixture(root);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        int a = inventory.EquippedA, b = inventory.EquippedB, ring = inventory.ActiveRing, health = inventory.HealthQuarters;
        byte spinnerState = memory.ReadField("wSpinnerState");
        bool crystals = rooms.SaveData.HasGlobalFlag(0x0f);
        void Load(int group, int room)
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
        }
        try
        {
            rooms.SaveData.SetGlobalFlag(0x0f, false);
            memory.SetField("wSpinnerState", 0xa0);
            Load(4, game == OracleGame.Ages ? 0x60 : 0x26);
            var room = rooms.CurrentRoom;
            var spinner = entities.Entities<DungeonSpinnerRoomEntity>().Single();
            FailIf(entities.InteractionSlot(spinner) != 2 || spinner.Position != new Vector2(120, 88),
                $"{game}: the actual room must compose INTERAC_SPINNER $7d in native slot$d2.");
            inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
            inventory.SetScriptedHealth(inventory.MaxHealthQuarters); session.StatusBar.SynchronizeHealth();
            player.WarpTo(new(120.25f, 40.5f)); player.Face(Vector2I.Down);
            memory.SetField("wDisabledObjects", 0); memory.SetField("wLinkForceState", 0); memory.SetField("wcc50", 0);
            FailIf(room.IsSolid(player.Position), $"{game}: spinner approach must begin on the actual north floor.");
            var native = new BraceletRom(room, player.PrecisePosition, 2, 0xff, game: game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native.SetNativeByte(memory.FieldAddress("wLinkStateParameter"),memory.ReadField("wLinkStateParameter"));
            native[0xcd00] = memory.ReadField("wScrollMode");
            native[0xd240] = 1; native[0xd241] = 0x7d; native[0xd24b] = 0x57; native[0xd24d] = 1;
            native.SetNativeByte(memory.FieldAddress("wSpinnerState"), 0xa0);
            var seed = entities.Random.CaptureState();
            native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            memory.SetField("wcc95", 0x80); native.SetNativeByte(memory.FieldAddress("wcc95"), 0x80);
            FailIf(player.NativeObjectVulnerable || native.LinkVulnerable(),
                $"{game}: checkLinkVulnerable must consume the live movement-lock signal.");
            memory.SetField("wcc95", 0); native.SetNativeByte(memory.FieldAddress("wcc95"), 0);
            // Arrange an already-published one-quarter contact before the lock.
            // Movement suppression cannot postpone state01's damage/palette phase.
            FailIf(!player.ApplyEnemyContactDamage(player.Position - Vector2.Right * 8, 1,
                RingDamageSource.Generic, invincibilityFrames: 0, knockbackFrames: 0) ||
                (player.PendingContactDamageRaw & 0xff) != 0xfe,
                $"{game}: pending contact fixture must publish native damage$fe.");
            native[0xd025] = 0xfe; native[0xd02a] = 1; native[0xd02c] = 8;
            memory.SetField("wcc95", 0x80); native.SetNativeByte(memory.FieldAddress("wcc95"), 0x80);
            var sounds = root._sound.AttachPlayRequestAudit();
            var image = ValidationRom.LoadCleanUs(game);
            int updates = 0;
            bool externalSignal = true;
            string Oam(int slot)
            {
                int offset = (game == OracleGame.Ages ? 0x50000 : 0x4c000) + (native.Word(slot + 0x1e) & 0x3fff);
                int count = image.Span[offset];
                FailIf(count is < 1 or > 40, $"{game}: invalid native spinner OAM count.");
                return string.Join(';', Enumerable.Range(0, count).Select(cell =>
                    string.Join(',', Enumerable.Range(0, 4).Select(part => image.Span[offset + 1 + cell * 4 + part]))));
            }
            var visual = new DungeonSpinnerVisualDatabase(rooms.AssetRoot).Visual;
            void Step(int count = 1, Vector2 movement = default) => app.Step(count, movement, batched: batched, afterUpdate: () =>
            {
                int keys = movement == Vector2.Down ? 0x80 : movement == Vector2.Right ? 0x10 : 0;
                native.UpdateLinkItems(entities.FrameCounter, keys, interactions: true);
                native.AdvanceScreenShake();
                string context = $"{game}/spinner/batch{batched}/update{++updates}";
                Vector2 position = new(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f);
                int phase = native[0xd244];
                if (phase == 1 && !externalSignal && (native.ReadNativeByte(memory.FieldAddress("wcc95")) & 0x80) != 0) phase = 2;
                FailIf(player.PrecisePosition != position || (int)spinner.Phase + 1 != phase ||
                    player.NativeForcedMovementActive != (native[0xd004] == 0x0b) ||
                    player.NativeState08Active != (native[0xd004] == 8),
                    context + $": XY/state runtime{player.PrecisePosition}/{spinner.Phase}/{player.NativeForcedMovementActive}/{player.NativeState08Active}, native{position}/{phase}/${native[0xd004]:x2}.");
                foreach (string field in new[] { "wLinkForceState", "wLinkStateParameter", "wcc50", "wDisabledObjects", "wSpinnerState" })
                    FailIf(memory.ReadField(field) != native.ReadNativeByte(memory.FieldAddress(field)),
                        context + $": native {field} differs: ${memory.ReadField(field):x2}/${native.ReadNativeByte(memory.FieldAddress(field)):x2}.");
                FailIf((memory.ReadField("wcc95") & 0x80) != (native.ReadNativeByte(memory.FieldAddress("wcc95")) & 0x80),
                    context + ": spinner signal lifetime differs.");
                FailIf(player.HealthQuarters != native[game == OracleGame.Ages ? 0xc6aa : 0xc6a2] ||
                    (player.PendingContactDamageRaw & 0xff) != native[0xd025],
                    context + ": movement suppression must preserve the earlier native damage-consumption phase.");
                foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        context + $": Link animation${address:x4} differs ${memory.ReadWramByte(address):x2}/${native[address]:x2}.");
                var body = SomariaPrivate<EnemyAnimationPlayer>(spinner, "_spinnerAnimation");
                string bodyOam = OracleGraphicsCache.GetAnimationDefinition(visual.Animations[body.AnimationIndex]).Frames[body.FrameIndex].EncodedOam;
                FailIf(body.CurrentParameter != native[0xd261] || SomariaPrivate<int>(body, "_frameCounter") != native[0xd260] || bodyOam != Oam(0xd240),
                    context + ": parent animation must retain this game's original OAM and clock.");
                FailIf(spinner.Arrow is not { } || entities.InteractionSlot(spinner.Arrow) != 3,
                    context + ": the parent must allocate its native later arrow slot$d3.");
                var arrowAnimation = SomariaPrivate<EnemyAnimationPlayer>(spinner.Arrow!, "_animation");
                string arrowOam = OracleGraphicsCache.GetAnimationDefinition(visual.Animations[arrowAnimation.AnimationIndex]).Frames[arrowAnimation.FrameIndex].EncodedOam;
                FailIf(arrowAnimation.CurrentParameter != native[0xd361] || SomariaPrivate<int>(arrowAnimation, "_frameCounter") != native[0xd360] || arrowOam != Oam(0xd340),
                    context + ": arrow animation must retain this game's original OAM and clock.");
                var rng = entities.Random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls - seed.Calls != native.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(native.Sounds),
                    context + $": ordered RNG/cues differ: {rng.Rng1:x2}/{rng.Rng2:x2}/{rng.Calls-seed.Calls} vs {native[0xff94]:x2}/{native[0xff95]:x2}/{native.RandomCalls}, [{string.Join(',',sounds.Requests)}]/[{string.Join(',',native.Sounds)}].");
            });
            Step();
            FailIf(player.PendingContactDamageRaw != 0, $"{game}: normal Link must consume the pending contact despite wcc95 bit7.");
            memory.SetField("wcc95", 0); native.SetNativeByte(memory.FieldAddress("wcc95"), 0);
            externalSignal = false;
            Step(33); Step(33, Vector2.Down);
            FailIf(spinner.Phase != SpinnerPhase.Touched, $"{game}: actual north-floor approach must touch the spinner.");
            for (int wait = 0; spinner.Phase != SpinnerPhase.Turning && wait < 8; wait++) Step();
            root._scene.Dialogue.ShowMessage("Spinner pause.", 104); native[0xcba0] = 1;
            Step(4);
            root._scene.Dialogue.Close(); native[0xcba0] = 0;
            for (int wait = 0; spinner.Phase != SpinnerPhase.Exiting && wait < 80; wait++) Step();
            FailIf(spinner.Phase != SpinnerPhase.Exiting || player.NativeForcedMovementActive,
                $"{game}: terminal spinner animation must publish a deferred Link state$0b request.");
            Step();
            memory.SetField("wDisabledObjects", 2); native.SetNativeByte(memory.FieldAddress("wDisabledObjects"), 2);
            int retainedCounter = spinner.ExitCounter;
            Step(4);
            FailIf(spinner.ExitCounter != retainedCounter || !player.NativeForcedMovementActive,
                $"{game}: freezing the interaction must retain its own counter while Link's selected state$0b keeps moving.");
            memory.SetField("wDisabledObjects", 0); native.SetNativeByte(memory.FieldAddress("wDisabledObjects"), 0);
            for (int wait = 0; spinner.Phase != SpinnerPhase.Waiting && wait < 32; wait++) Step();
            FailIf(!spinner.Red || spinner.Phase != SpinnerPhase.Waiting, $"{game}: first shared native turn/exit did not complete.");
            Step(32);
            for (int walk = 0; spinner.Phase == SpinnerPhase.Waiting && walk < 40; walk++) Step(1, Vector2.Right);
            FailIf(spinner.Phase != SpinnerPhase.Touched, $"{game}: repeat must approach through the actual west floor.");
            for (int wait = 0; spinner.Phase != SpinnerPhase.Waiting && wait < 80; wait++) Step();
            Step(2);
            FailIf(spinner.Red || memory.ReadField("wSpinnerState") != 0xa0 || player.NativeForcedMovementActive || player.NativeState08Active,
                $"{game}: repeated shared spinner handoff must restore only its mask and release Link.");
            if (game == OracleGame.Seasons)
                foreach (var (group, roomId, position) in new[] { (4, 0xb1, new Vector2(120, 88)), (5, 0x6f, new Vector2(120, 104)) })
                {
                    Load(group, roomId);
                    var placed = entities.Entities<DungeonSpinnerRoomEntity>().Single();
                    FailIf(placed.Position != position || placed.AssetRoot != rooms.AssetRoot || entities.InteractionSlot(placed) != 2,
                        $"Seasons: native spinner-only stream ${group:x1}:${roomId:x2} lost its selected-pack constructor/slot/packed position.");
                }
        }
        finally
        {
            rooms.SaveData.SetGlobalFlag(0x0f, crystals);
            memory.SetField("wSpinnerState", spinnerState);
            inventory.EquipA(a); inventory.EquipB(b); inventory.SetScriptedActiveRing(ring);
            inventory.SetScriptedHealth(health); session.StatusBar.SynchronizeHealth();
            Load(originalGroup, originalRoom);
        }
    }
}
