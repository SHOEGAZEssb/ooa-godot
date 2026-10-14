using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task ValidatePostD7Handoff()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            for (int i = 0; i < 7; i++) _inventory.GiveTreasure(TreasureId.Essence, i);
            _saveData.SetMakuTreeState(6);
            _saveData.SetRoomFlag(2, 0x90, 2);
            _saveData.SetGlobalFlag(GlobalFlag.GotPermissionToEnterJabu);
            LoadValidationRoom(2, 0x90);
            _player.WarpTo(new(72, 72), recordSafe: false);
            // essence.s state7 clears wActiveMusic before the return warp.
            _sound.ClearActiveMusic();
            var sounds = _sound.AttachPlayRequestAudit();
            var owner = _roomEvents.Get<PostD7Event>();
            var remote = _roomEvents.Get<RemoteMakuSeventhEssenceEvent>();
            var texts = new List<int>();
            int hold = 0, updates = 0;
            bool sawTower = false, sawAscent = false, sawStone = false;
            int towerFadeStart = -1;
            void Observe()
            {
                updates++;
                FailIf(sounds.Requests.Contains(SoundId.SndEnterCave),
                    "Post-D7 direct room loads and func_6733's $03 white fade must not dispatch SND_ENTERCAVE $6e.");
                if (!linked && owner.Stage == PostD7Stage.Complete && owner.State == 5)
                    FailIf(!_hud.StatusBarHidden || _hud.HiddenStatusBarColorForValidation != Colors.Black || _player.Visible,
                        "Post-D7 cloaked Twinrova scene must retain the black room BG across the status strip with no Link.");
                if (owner.Stage == PostD7Stage.Tower && owner.State > 0)
                {
                    sawTower = true;
                    FailIf(_player.Visible || _hud.Visible,
                        "Post-D7 gfx-register state $09 must show the full tower panorama without Link or a HUD strip.");
                    if (IsTransitioning)
                    {
                        if (towerFadeStart < 0) towerFadeStart = updates;
                        FailIf(SomariaPrivate<BlackTowerExplanationScreen>(owner, "_tower") is null ||
                            _entities.RuntimeState.ReadWramByte(0xcbb3) != 0 ||
                            !Mathf.IsEqualApprox(_warpFade.Color.A, Math.Min(updates - towerFadeStart, 31) / 31f),
                            "Post-D7 $03 must retain the tower panorama and freeze its handler during the 32-update white fade.");
                    }
                }
                if (owner.Stage == PostD7Stage.Ascent)
                {
                    if (!sawAscent)
                        FailIf(towerFadeStart < 0 || updates - towerFadeStart != 32,
                            "Post-D7 $4:$f6 must load only on the terminal update of the source's 32-update white fade.");
                    sawAscent = true;
                    FailIf(_player.Visible || !_hud.Visible || _hud.StatusBarHidden,
                        "Post-D7 $4:$f6 arrival $0f must keep Link invisible and restore the room HUD.");
                }
                if (owner.Stage == PostD7Stage.Stone && owner.State is > 0 and < 6)
                {
                    sawStone = true;
                    FailIf(_player.Visible || !_hud.Visible || _hud.StatusBarHidden,
                        "Post-D7 stone vignettes must keep Link absent with the status bar shown.");
                }
                if (owner.Stage is PostD7Stage.Tower or PostD7Stage.Ascent ||
                    owner.Stage == PostD7Stage.Stone && owner.State is > 0 and < 6)
                    FailIf(_sound.ActiveMusic != 0x21 || _sound.NativeActiveMusic != 0,
                        $"Post-D7 {owner.Stage}/${owner.State:x2} must retain MUS_DISASTER $21 and the cleared room-music gate.");
                if (_dialogue.IsOpen && hold == 0)
                { texts.Add(_dialogue.SourceTextId ?? -1); hold = 3; }
                if (hold > 0 && --hold == 0) _dialogue.Close();
            }
            for (int i = 0; i < 6000 && (owner.HasState || remote.HasState || i < 2); i += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Observe);
            int[] expected = linked
                ? [0x2809, 0x280a, 0x130b, 0x130c, 0x130d, 0x05c9]
                : [0x2808, 0x130b, 0x130c, 0x130d, 0x05b9];
            FailIf(owner.HasState || remote.HasState || !sawTower || !sawAscent || !sawStone || !texts.SequenceEqual(expected),
                $"Post-D7 linked={linked}, batch={batch}, update={updates}, stage={owner.Stage}, state={owner.State}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, signals={_entities.RuntimeState.ReadWramByte(0xcfc0):x2}/{_entities.RuntimeState.ReadWramByte(0xcfdf):x2}, texts={string.Join(',', texts.Select(x => x.ToString("x4")))}.");
            FailIf(_rooms.ActiveGroup != 2 || _rooms.CurrentRoom.Id != 0x90 ||
                !_saveData.HasRoomFlag(4, 0xf6, 0x40) || !_saveData.HasRoomFlag(2, 0x90, 0x40) ||
                _saveData.MakuTreeState != 7 || _saveData.MakuMapTextPresent != (linked ? 0xc9 : 0xb9) ||
                !_player.Visible || !_hud.Visible || _hud.StatusBarHidden || _player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0,
                "Post-D7 must persist the tower and remote-Maku flags and return control to Link in $2:$90.");
            LoadValidationRoom(2, 0x90); StepGameplayUpdates(4, Vector2.Zero);
            FailIf(owner.HasState || remote.HasState, "Completed post-D7 handoff must not replay on room re-entry.");
        }
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 6);
            LoadValidationRoom(2, 0x90);
            var owner = _roomEvents.Get<PostD7Event>();
            var sounds = _sound.AttachPlayRequestAudit();
            for (int wait = 0; !IsTransitioning && wait < 1800; wait += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: () =>
                { if (_dialogue.IsOpen) _dialogue.Close(); });
            FailIf(!IsTransitioning || owner.Stage != PostD7Stage.Tower,
                "Post-D7 cancellation fixture must reach the tower-to-interior white fade.");
            StepGameplayUpdates(12, Vector2.Zero, batched: batch);
            LoadValidationRoom(1, 0x36);
            StepGameplayUpdates(40, Vector2.Zero, batched: batch);
            FailIf(IsTransitioning || owner.HasState ||
                SomariaPrivate<BlackTowerExplanationScreen>(owner, "_tower") is not null ||
                _rooms.ActiveGroup != 1 || _rooms.CurrentRoom.Id != 0x36 ||
                !_player.Visible || !_hud.Visible || _hud.StatusBarHidden || _player.CutsceneControlled ||
                _warpFade.Color.A != 0 || sounds.Requests.Contains(SoundId.SndEnterCave) ||
                _saveData.HasRoomFlag(4, 0xf6, 0x40) || _saveData.HasRoomFlag(2, 0x90, 0x40),
                $"Cancelling the tower fade must release its panorama/transition without a late interior load, entrance cue or completion flags: " +
                $"transition={IsTransitioning}, stage={owner.Stage}, tower={SomariaPrivate<BlackTowerExplanationScreen>(owner, "_tower") is not null}, " +
                $"room=${_rooms.ActiveGroup:x1}:${_rooms.CurrentRoom.Id:x2}, Link={_player.Visible}/{_player.CutsceneControlled}, " +
                $"HUD={_hud.Visible}/{_hud.StatusBarHidden}, fade={_warpFade.Color.A}, cues={string.Join(',', sounds.Requests)}, " +
                $"flags=${_saveData.GetRoomFlags(4, 0xf6):x2}/${_saveData.GetRoomFlags(2, 0x90):x2}.");
            LoadValidationRoom(2, 0x90);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(!owner.HasState,
                "Cancelled post-D7 must remain eligible when its source room is re-entered.");
        }
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.Essence, 6); LoadValidationRoom(2, 0x90);
        for (int wait = 0; !_dialogue.IsOpen && wait < 300; wait++) StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != 0x2808 || !_hud.StatusBarHidden ||
            _hud.HiddenStatusBarColorForValidation != Colors.Black,
            "Post-D7 cancellation fixture must reach cloaked Twinrova's TX_2808 without the HUD strip.");
        string captureDirectory = OS.GetEnvironment("OOA_POST_D7_SCREENSHOT_OUTPUT");
        if (!string.IsNullOrEmpty(captureDirectory))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            image.SavePng(System.IO.Path.Combine(captureDirectory, "cloaked-twinrova.png"));
            FailIf(image.GetPixel(image.GetWidth() / 2, 0) != Colors.Black,
                "Rendered cloaked Twinrova scene must have a black top edge without a yellow HUD strip or clear-color margin.");
        }
        _dialogue.Close(); LoadValidationRoom(1, 0x36); StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_roomEvents.Get<PostD7Event>().HasState || !_player.Visible || !_hud.Visible || _hud.StatusBarHidden ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
            "Cancelling post-D7 must release its palette, HUD and input ownership.");
        if (!string.IsNullOrEmpty(captureDirectory))
        {
            LoadValidationRoom(4, 0xf6);
            for (int wait = 0; _dialogue.SourceTextId != 0x130c && wait < 700; wait++)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                if (_dialogue.IsOpen && _dialogue.SourceTextId == 0x130b) _dialogue.Close();
            }
            FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != 0x130c,
                "Rendered post-D7 fixture must reach Ambi's TX_130c with sparkle $84:$08.");
            StepGameplayUpdates(60, Vector2.Zero);
            var ring = _entities.EntityAdapters<PostD7ActorRoomEntity>().Single(actor => actor.Id == 0x84);
            if (!ring.Node.Visible) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(((NpcCharacter)ring.Node).CurrentAnimationTextureSize != new Vector2I(48, 48),
                "Ambi's circular darkness effect must retain its full source 48x48 OAM bounds.");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            image.SavePng(System.IO.Path.Combine(captureDirectory, "ambi-darkness.png"));
            _dialogue.Close(); LoadValidationRoom(1, 0x36);
        }
    }
}
