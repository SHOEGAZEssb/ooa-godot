using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomOfRitesRitualRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf1, 0x80, false);
            _saveData.AddRoomFlagByte(5, 0x60, 0xf7 - _saveData.GetRoomFlags(5, 0x60));
            LoadValidationRoom(5, 0xf1);
            _player.WarpTo(new(0x78, 0x88)); _player.Face(Vector2I.Up);
            var owner = _roomEvents.Get<RoomOfRitesZeldaEvent>();
            var zelda = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xad);
            var trigger = _entities.EntityAdapters<ZeldaApproachRoomEntity>().Single();
            var flames = _entities.EntityAdapters<TwinrovaFlameRoomEntity>().ToArray();
            FailIf(flames.Length != 3 || flames.Select(f => f.Subid).SequenceEqual(new[] { 3, 4, 5 }) == false,
                "Room $5:$f1 requires the ordered $a9:$03/$04/$05 ritual flames.");
            FailIf(_entities.InteractionSlot(zelda) != 2 || _entities.NativeInteractionSlot(trigger) != 3 ||
                !flames.Select(f => _entities.NativeInteractionSlot(f)).SequenceEqual(new[] { 4, 5, 6 }),
                "Room $5:$f1 must allocate Zelda, approach trigger and flames in the original $d2-$d6 slot order.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x88);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int za = Address(_entities.InteractionSlot(zelda));
            int ta = Address(_entities.NativeInteractionSlot(trigger));
            rom[za] = 1; rom[za + 1] = 0xad; rom[za + 11] = 0x42; rom[za + 13] = 0x78;
            rom[ta] = 1; rom[ta + 1] = 0xda; rom[ta + 11] = 0x5c; rom[ta + 13] = 0x78;
            foreach (var flame in flames)
            {
                int at = Address(_entities.NativeInteractionSlot(flame));
                rom[at] = 1; rom[at + 1] = 0xa9; rom[at + 2] = (byte)flame.Subid;
            }
            var animations = flames.Select(f => (EnemyAnimationPlayer)typeof(DungeonInteractionVisualEntity)
                .GetField("_animation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f)!).ToArray();
            var counter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var amplitude = typeof(RoomView).GetField("_waveAmplitude", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var phase = typeof(RoomView).GetField("_wavePhase", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var sounds = new ValidationSoundRequestAudit(); _sound.SetRequestObserver(sounds);
            int updates = 0;
            StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () => Native(false));
            FailIf(_saveData.GetRoomFlags(5, 0x60) != 0x27 || flames.Any(f => !f.Visible),
                "Ritual flames must wrap the source room-$60 flag byte by $10+$10+$10 and reveal on initialization.");
            rom[0xcc8a] = 2; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 2);
            StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () => Native(false));
            rom[0xcc8a] = 0; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
            // Walk the actual center aisle; the trigger fires before Zelda's
            // A range. Do not teleport inside the altar or NPC hitbox.
            for (int i = 0; i < 100 && !owner.WarpActive; i++)
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"], batched: batched, afterUpdate: () => Native(true));
            FailIf(!owner.WarpActive || !trigger.Finished || _player.Position.Y > 0x5c || flames.Any(f => f.Visible) ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x17 || _dialogue.IsOpen,
                $"Room $5:$f1 actual aisle approach failed: active={owner.WarpActive}, trigger={trigger.Finished}, Link={_player.Position}, nativeLink={rom[0xd00d]},{rom[0xd00b]}, visible={flames.Count(f=>f.Visible)}, vulnerable={_player.NativePuzzleResetVulnerable}, menu={_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled):x2}.");
            Vector2 link = _player.Position;
            Vector2 origin = -_transitions.WorldToGameplayScreen(Vector2.Zero);
            rom[0xffaa] = (byte)origin.Y; rom[0xffac] = (byte)origin.X;
            int warpUpdates = 0;
            do
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    rom.AdvanceRitualWarp(_entities.FrameCounter); ++warpUpdates;
                    FailIf(owner.WarpState != rom[0xcc03] || _player.Position != link ||
                        _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0),
                        $"Ritual warp update{warpUpdates}: state/fade/Link differs: {owner.WarpState}/{rom[0xcc03]}, palette={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}, Link={_player.Position}/{link}.");
                    Compare();
                    if (warpUpdates > 1 && owner.WarpActive)
                    {
                        var background = SomariaPrivate<Texture2D>(_roomView, "_waveBackground");
                        FailIf(background.GetWidth() != 256 || background.GetHeight() != _currentRoom.Height,
                            "Ritual SCX must wrap the original 256-pixel padded tilemap, independently of the room's playable width.");
                        int a = (int)amplitude.GetValue(_roomView)!, p = (int)phase.GetValue(_roomView)!;
                        foreach (int y in new[] { 0, 1, 15, 31, 63, 64, 100, 127 })
                        {
                            int sample = p + (y + (int)origin.Y) * 2;
                            int offset = (sample & 1) == 0 ? (int)origin.X : ScreenWaveData.Shared.Offset(a, sample);
                            FailIf(unchecked((byte)offset) != rom[0xc300 + y],
                                $"Ritual warp update{warpUpdates}: native wave sample{y} differs, amplitude={a},phase={p}, offset={offset}/{rom[0xc300+y]}.");
                        }
                    }
                    foreach (int address in new[] { 0xcbb3, 0xcbb4, 0xcbb5, 0xcbb7 })
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                            $"Ritual warp update{warpUpdates}: shared ${address:x4} differs: {_runtimeState.ReadWramByte(address):x2}/{rom[address]:x2}.");
                });
            } while (owner.WarpActive && warpUpdates < 600);
            _sound.SetRequestObserver(null);
            Warp warp = SomariaPrivate<Warp>(_transitions, "_pendingWarp");
            FailIf(warp.DestinationGroup != 5 || warp.DestinationRoom != 0xf5 || warp.DestinationPosition != 0x77 ||
                warp.DestinationParameter != 0 || warp.DestinationTransition != WarpDestinationTransition.Fall ||
                rom[0xcc47] != 0x85 || rom[0xcc48] != 0xf5 || rom[0xcc49] != 5 || rom[0xcc4a] != 0x77 || rom[0xcc4b] != 0,
                "Ritual handoff must preserve original hardcoded warp $85,$f5,$05,$77,$00.");
            FailIf(owner.WarpActive || !_transitions.IsTransitioning || !sounds.Requests.SequenceEqual(rom.Sounds),
                $"Ritual warp failed its bounded native handoff/sounds: updates={warpUpdates}, sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            owner.Cancel(); owner.Cancel();
            FailIf(owner.HasState || _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "Ritual repeated cancellation retained event/menu ownership.");

            void Native(bool moving)
            {
                rom.UpdateGameplay(0, moving ? 0x40 : 0, moving ? 0 : 0xff, _entities.FrameCounter); ++updates;
                Compare();
                FailIf(trigger.Finished != (rom[ta] == 0) || _saveData.GetRoomFlags(5, 0x60) != rom[0xca60],
                    $"Ritual update{updates}: trigger={trigger.State}/{trigger.Finished} vs {rom[ta + 4]:x2}/{rom[ta]:x2}, room$60={_saveData.GetRoomFlags(5, 0x60):x2}/{rom[0xca60]:x2}.");
            }
            void Compare()
            {
                for (int i = 0; i < flames.Length; i++)
                {
                    var flame = flames[i]; int at = Address(_entities.NativeInteractionSlot(flame));
                    FailIf(flame.Position != new Vector2(rom[at + 13], rom[at + 11]) || flame.State != rom[at + 4] ||
                        flame.Visible != ((rom[at + 0x1a] & 128) != 0) ||
                        (int)counter.GetValue(animations[i])! != rom[at + 0x20],
                        $"Ritual flame ${flame.Subid:x2}, update{updates}: native state, position, visibility or animation phase differs.");
                }
                FailIf(zelda.Visible != ((rom[za + 0x1a] & 128) != 0) || zelda.CurrentAnimationCounter != rom[za + 0x20] ||
                    RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Ritual update{updates}: Zelda flicker, animation or shared RNG differs.");
            }
        }
        GD.Print("Validated ritual flame initialization/save-byte overflow, actual aisle approach, native warp counters, interaction-only double pass, fade, frozen Link and handoff in split/batched gameplay.");
        static int Address(int slot) => 0xd040 + slot * 256;
    }
}
