using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombEssenceRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            for (int i = 0; i < 7; i++) _inventory.GiveTreasure(TreasureId.Essence, i);
            LoadValidationRoom(5, 0x77);
            _saveData.SetMapleKillCounter(30);
            var essence = _entities.Entities<DungeonEssence>().Single();
            FailIf(essence.EssenceIndex != 7 || essence.Position != new Vector2(120, 40) ||
                !essence.Message.Contains("Falling Star", StringComparison.Ordinal) ||
                essence.ExitWarp is not { DestinationGroup: 1, DestinationRoom: 0x5c, DestinationPosition: 0x15, DestinationTransition: 0x0e },
                "Falling Star must resolve source index$07, TX_0015 and exit$1:$5c/$15/$0e.");
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(120, 120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Falling Star must be approached through the original south floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 120) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            int s = 0xd040 + _entities.InteractionSlot(essence) * 256;
            rom[s] = 1; rom[s + 1] = 0x7f; rom[s + 11] = 40; rom[s + 13] = 120;
            int update = 0;
            void Step(int count = 1, bool approach = false) => StepGameplayUpdates(count, approach ? Vector2.Up : Vector2.Zero,
                batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, approach ? 0x40 : 0, approach ? 0 : 0xff, _entities.FrameCounter);
                    rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"Falling Star update{update}");
                    FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) ||
                        _inventory.Essences != rom[AgesWramAddress.wEssencesObtained] || _saveData.GetRoomFlags(5, 0x77) != rom[0xca77] ||
                        essence.EssenceIndex != rom[s + 3] || essence.Visible != ((rom[s + 26] & 0x80) != 0) ||
                        SomariaPrivate<Vector2>(essence, "_precisePosition") != new Vector2(rom.Word(s + 12) / 256f, rom.Word(s + 10) / 256f) ||
                        (SomariaPrivate<int>(essence, "_zFixed") & 0xffff) != rom.Word(s + 14),
                        $"Falling Star update{update}: collection/motion/flags or energy RNG differs.");
                });
            for (int walk = 0; !_dialogue.IsOpen && walk < 240; walk++) Step(approach: true);
            FailIf(!_dialogue.IsOpen || rom[s + 4] != 5 || rom.Word(0xcba2) != 0x0415 || _inventory.Essences != 0xff,
                "The original pedestal approach must grant Essence bit$80 and show TX_0015.");
            Step(8); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; !IsTransitioning && wait < 600; wait++) Step();
            FailIf(!IsTransitioning || rom[0xcc47] != 0x81 || rom[0xcc48] != 0x5c || rom[0xcc4a] != 0x15 ||
                rom[0xcc49] != 0x0e || rom[0xcc4b] != 0x83,
                "Falling Star must publish its source X-shifted return after the shared Essence waits.");
            for (int wait = 0; IsTransitioning && wait < 180; wait++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(IsTransitioning || _rooms.ActiveGroup != 1 || _rooms.CurrentRoom.Id != 0x5c ||
                !_roomEvents.Get<RemoteMakuEighthEssenceEvent>().HasState,
                "The actual Essence warp must admit past remote Maku $8a:$01/v$0a in room$1:$5c.");
            int mapleState = _saveData.MapleState;
            int makuState = _saveData.MakuTreeState;
            var mapleNative = new BraceletRom(_currentRoom, _player.Position, 0, _inventory.ActiveRing);
            mapleNative.LoadSave(_saveData);
            mapleNative.CheckAndSpawnMaple();
            FailIf(mapleNative[0xc641] != 30 || mapleNative[0xcdda] != 0 || _entities.MaplePresent(),
                "The original past-$5c bitmap blocks Maple even at 30 kills on the actual D8 return.");
            for (int wait = 0; _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81 && wait < 4; wait++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_saveData.MapleKillCounter != 30 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x80,
                "D8 return must preserve the blocked Maple threshold, then publish remote Maku's $81/$80 input masks.");
            void CheckMapleSuppressed() => FailIf(_entities.MaplePresent() || _saveData.MapleKillCounter != 30 ||
                _saveData.MapleState != mapleState || _sound.ActiveMusic == SoundId.MusMapleTheme,
                "The blocked Maple encounter must remain absent throughout D8 Maku, including dialogue.");
            for (int wait = 0; !_dialogue.IsOpen && wait < 1000; wait += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: CheckMapleSuppressed);
            FailIf(!_dialogue.IsOpen || _saveData.MakuMapTextPast != 0xba ||
                !_dialogue.CurrentMessage.Contains("Please visit me", StringComparison.Ordinal),
                "Eighth-Essence handoff must reach Maku's source TX_05ba.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batch, afterUpdate: CheckMapleSuppressed);
            _dialogue.Close();
            var remote = _roomEvents.Get<RemoteMakuEighthEssenceEvent>();
            for (int wait = 0; remote.HasState && wait < 180; wait += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: () =>
                {
                    if (remote.HasState) CheckMapleSuppressed();
                });
            FailIf(remote.HasState || _player.CutsceneControlled || _hud.StatusBarHidden || !_saveData.HasRoomFlag(1,0x5c,0x40) ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0 || _saveData.MakuTreeState != makuState + 1,
                "Eighth Maku handoff must persist completion and restore input/HUD.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batch, afterUpdate: CheckMapleSuppressed);
            LoadValidationRoom(1, 0x5c); StepGameplayUpdates(3, Vector2.Zero);
            FailIf(remote.HasState, "Completed eighth Maku message must not replay on re-entry.");
            LoadValidationRoom(5, 0x77); StepGameplayUpdates(3, Vector2.Zero);
            var collected = _entities.Entities<DungeonEssence>().Single();
            FailIf(!collected.Collected || collected.Visible, "Falling Star must remain collected on re-entry.");
        }
        GD.Print("Validated clean-US Falling Star pickup, grant, motion/RNG, waits and return warp, plus eighth Maku handoff and re-entry.");
    }
}
