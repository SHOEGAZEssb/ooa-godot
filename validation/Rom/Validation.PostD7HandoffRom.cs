using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7HandoffRom()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.Essence, 6);
            _saveData.SetRoomFlag(2, 0x90, 2);
            LoadValidationRoom(2, 0x90); _player.WarpTo(new(72, 72), recordSafe: false);
            _sound.ClearActiveMusic();
            var sounds = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero);
            var owner = _roomEvents.Get<PostD7Event>();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 72, 72)
                { HostilePartsEnabled = true };
            var palettes = rom.CreateMenuView();
            // State2 must clear actual nonblack source bytes before flashScreen restores them.
            for (int palette = 2; palette < 8; palette++) palettes.SeedBackgroundPalette(palette, [0x7fff, 0x7fff, 0x7fff, 0x7fff]);
            rom.InitializeLinkGameplay(); rom[0xcc01] = (byte)(linked ? 1 : 0);
            rom[0xcc35] = 0; // essence.s state7, retained throughout this native sequence.
            rom[0xc2ef] = 0x21; rom[0xcc03] = 0; rom[0xcc8a] = rom[0xcc02] = 1;
            int hold = 0, generation = 0, update = 1, flashGaps = 0;
            void Compare()
            {
                update++;
                rom.AdvanceWarpPalette();
                if (rom[0xcc4b] != 0)
                {
                    // A batched host frame may contain the next main-thread
                    // update after publication. cutscene08 consumes the request
                    // here; neither its handler nor Link's source path runs.
                    rom.ApplyRequestedWarp();
                    FailIf(sounds.Requests.Contains(SoundId.SndEnterCave) ||
                        owner.NativeActive || owner.Stage != PostD7Stage.Tower,
                        "Post-D7 pending $03 must hand the white fade to the transition owner without an entrance cue.");
                    return;
                }
                if (rom[0xcc04] != 0)
                {
                    rom.SelectTriggeredCutscene();
                    rom[0xcc00] = (byte)_entities.FrameCounter;
                }
                else
                {
                    rom.AdvancePostD7();
                    rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                }
                var rng = _random.CaptureState();
                if (owner.Stage == PostD7Stage.Complete && (linked ? owner.State is 3 or 4 : owner.State == 5))
                {
                    FailIf(_hud.Visible || !_transitions.NativeRoomUsesFullScreen || !_hud.StatusBarHidden || _hud.HiddenStatusBarColorForValidation != Colors.Black ||
                        _roomView.BackgroundFadeColorForValidation != Colors.Black || _player.Visible ||
                        rom[0xc48a] != 0 || rom[0xc48c] != 0 || rom[0xd000] != 0 && (rom[0xd01a] & 0x80) != 0,
                        $"Post-D7 update{update}: linked={linked} black backdrop must cover the HUD strip; HUD={_hud.Visible}/{_hud.StatusBarHidden}/{_hud.HiddenStatusBarColorForValidation}, BG={_roomView.BackgroundFadeColorForValidation}, Link={_player.Visible}/${rom[0xd000]:x2}:${rom[0xd01a]:x2}, native LYC=${rom[0xc48a]:x2}/SCY=${rom[0xc48c]:x2}.");
                    if (linked && owner.State == 4 && rom[0xcbba] == 1)
                    {
                        flashGaps++;
                        FailIf(_warpFade.Color.A != 0, "Linked post-D7 flash gaps must expose the black backdrop without a fullscreen white cover.");
                        for (int offset = 0; offset < 0x30; offset++)
                            FailIf(rom.BankByte(2, 0xde90 + offset) != 0,
                                $"Linked post-D7 flash gap must retain black native BG palette byte ${0xde90 + offset:x4}.");
                    }
                }
                if (linked && owner.Stage == PostD7Stage.Complete && owner.State == 5)
                    FailIf(!_hud.Visible || _transitions.NativeRoomUsesFullScreen || _hud.StatusBarHidden || _hud.HiddenStatusBarFadeAlphaForValidation != 0 ||
                        _roomView.BackgroundFadeAlpha != 0 || rom[0xc48a] != 0x0f || rom[0xc48c] != 0xf0,
                        "Linked post-D7 village cutaway must restore the HUD split and release the black backdrop.");
                FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds) ||
                    _sound.NativeActiveMusic != rom[0xcc35],
                    $"Post-D7 update{update}, linked={linked}: cues={string.Join(',', sounds.Requests)}/{string.Join(',', rom.Sounds)}, room-music gate=${_sound.NativeActiveMusic:x2}/${rom[0xcc35]:x2}.");
                if (owner.Stage == PostD7Stage.Tower && owner.State > 0)
                {
                    FailIf(_hud.Visible || _player.Visible ||
                        SomariaPrivate<int>(owner, "_scrollY") != rom[0xc486],
                        $"Post-D7 update{update}: full-screen tower SCY/Link/HUD differs from gfx-register state $09.");
                    foreach (int address in new[] { 0xcbb3, 0xcbb6, 0xcbb7, 0xcbb9, 0xcbba, 0xcbbb })
                        FailIf(_entities.RuntimeState.ReadWramByte(address) != rom[address],
                            $"Post-D7 update{update}: tower counter ${address:x4}={_entities.RuntimeState.ReadWramByte(address):x2}/{rom[address]:x2}.");
                }
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    $"Post-D7 update{update}, linked={linked}: RNG calls {rng.Calls - seed.Calls}/{rom.RandomCalls}, stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}.");
                if (owner.Stage != PostD7Stage.Ascent)
                    FailIf(owner.State != rom[0xcc03] || _dialogue.IsOpen != (rom[0xcba0] != 0) ||
                        _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0),
                        $"Post-D7 update{update}, linked={linked}: stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}, text={_dialogue.IsOpen}/{rom[0xcba0]}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                if (owner.Stage != PostD7Stage.Ascent)
                foreach (var actor in _entities.EntityAdapters<PostD7ActorRoomEntity>())
                {
                    int s = 0xd040 + slots[actor] * 256;
                    FailIf(rom[s + 1] != actor.Id || rom[s + 2] != actor.SubId || rom[s + 4] != actor.State || rom[s + 5] != actor.Substate,
                        $"Post-D7 update{update} slot${slots[actor]:x2}: port=${actor.Id:x2}:${actor.SubId:x2}/{actor.State}/{actor.Substate}, ROM=${rom[s + 1]:x2}:${rom[s + 2]:x2}/{rom[s + 4]}/{rom[s + 5]}.");
                    ComparePostD7AnimationRom(rom, actor, s, $"Post-D7 update{update}");
                    if (_transitions.NativeRoomUsesFullScreen)
                        CompareFullscreenActorPositionRom(rom, (NpcCharacter)actor.Node, s, $"Post-D7 update{update}");
                }
                if (generation != rom.TextGeneration)
                {
                    generation = rom.TextGeneration; hold = 3;
                    FailIf(_dialogue.SourceTextId + 0x0400 != rom.Word(0xcba2),
                        $"Post-D7 update{update}: text=${_dialogue.SourceTextId:x4}/${rom.Word(0xcba2):x4} (native primary-bank high index +$04).");
                }
                if (hold > 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
            }
            // Include the update publishing the warp: func_6733 writes the
            // destination/request directly without dispatching Link's source
            // handler. The room loader's placement RNG is a separate fixture.
            for (int limit = 0; !IsTransitioning && limit < 1800; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(!IsTransitioning || owner.Stage != PostD7Stage.Tower || rom[0xcc47] != 0x84 || rom[0xcc48] != 0xf6 ||
                rom[0xcc49] != 0x0f || rom[0xcc4a] != 0x47 ||
                !(rom[0xcc4b] == 3 || rom[0xc2ef] == 3),
                $"The native completed-tower scroll must publish invisible transition $0f to room $4:$f6/$47, parameter $03: stage={owner.Stage}, warp=${rom[0xcc47]:x2}/${rom[0xcc48]:x2}/${rom[0xcc49]:x2}/${rom[0xcc4a]:x2}/${rom[0xcc4b]:x2}.");
            FailIf(linked && flashGaps != 6, $"Linked post-D7 must compare all six uncovered lightning-flash updates; observed {flashGaps}.");
        }
    }

    private void ComparePostD7AnimationRom(SomariaRom rom, PostD7ActorRoomEntity entity, int slot, string context)
    {
        if (entity.State == 0) return;
        CompareInteractionOamRom(rom, (NpcCharacter)entity.Node, slot,
            $"{context} ${entity.Id:x2}:${entity.SubId:x2}");
    }
}
