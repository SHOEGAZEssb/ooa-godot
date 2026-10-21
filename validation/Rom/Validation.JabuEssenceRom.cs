using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuEssenceRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            for (int i = 0; i < 6; i++) _inventory.GiveTreasure(TreasureId.Essence, i);
            _saveData.SetRoomFlag(2, 0x90, 2); LoadValidationRoom(5, 0x61);
            var essence = _entities.Entities<DungeonEssence>().Single();
            FailIf(essence.EssenceIndex != 6 || essence.Position != new Vector2(104, 40) ||
                !essence.Message.Contains("Rolling Sea", StringComparison.Ordinal) ||
                essence.ExitWarp is not { DestinationGroup: 2, DestinationRoom: 0x90, DestinationPosition: 0x45, DestinationTransition: 0x0e },
                "Source $7f seventh OAM/TX_0014/warp row must select Rolling Sea and $2:$90/$45/$0e.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(entity => entity != essence).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(104, 120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Rolling Sea approach must start on the source room's south floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 104, 120) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 7;
            int s = 0xd040 + _entities.InteractionSlot(essence) * 256;
            rom[s] = 1; rom[s + 1] = 0x7f; rom[s + 11] = 40; rom[s + 13] = 104;
            int update = 0;
            void Step(int count = 1, bool approach = false) => StepGameplayUpdates(count, approach ? Vector2.Up : Vector2.Zero,
                batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, approach ? 0x40 : 0, approach ? 0 : 0xff, _entities.FrameCounter);
                    rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"Rolling Sea update{update}");
                    FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) ||
                        _inventory.Essences != rom[WramAddress.wEssencesObtained] || _saveData.GetRoomFlags(5, 0x61) != rom[0xca61] ||
                        essence.EssenceIndex != rom[s + 3] || essence.Visible != ((rom[s + 26] & 0x80) != 0) ||
                        SomariaPrivate<Vector2>(essence, "_precisePosition") != new Vector2(rom.Word(s + 12) / 256f, rom.Word(s + 10) / 256f) ||
                        (SomariaPrivate<int>(essence, "_zFixed") & 0xffff) != rom.Word(s + 14),
                        $"Rolling Sea update{update}: native collection/motion/room flag or energy RNG differs, state={rom[s + 4]}, essences=${_inventory.Essences:x2}/${rom[WramAddress.wEssencesObtained]:x2}.");
                });
            for (int walk = 0; !_dialogue.IsOpen && walk < 240; walk++) Step(approach: true);
            FailIf(!_dialogue.IsOpen || rom[s + 4] != 5 || rom.Word(0xcba2) != 0x0414 || _inventory.Essences != 0x7f,
                "The real pedestal approach must grant only essence bit$40 and display source TX_0014.");
            Step(8); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; !IsTransitioning && wait < 600; wait++) Step();
            FailIf(!IsTransitioning || rom[0xcc47] != 0x82 || rom[0xcc48] != 0x90 || rom[0xcc4a] != 0x45 ||
                rom[0xcc49] != 0x0e || rom[0xcc4b] != 0x83,
                "Native Rolling Sea completion must publish X-shifted return $82:$90/$45/$0e after its shared essence waits.");
            for (int wait = 0; IsTransitioning && wait < 180; wait++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(IsTransitioning || _rooms.ActiveGroup != 2 || _rooms.CurrentRoom.Id != 0x90 ||
                !_roomEvents.Get<PostD7Event>().HasState,
                "The actual Rolling Sea warp must hand control to the source post-D7 sequence in $2:$90.");
            var postD7 = _roomEvents.Get<PostD7Event>();
            bool sawTower = false;
            for (int wait = 0; _dialogue.SourceTextId != 0x130b && wait < 2200; wait += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: () => {
                    if (postD7.Stage == PostD7Stage.Tower && postD7.State > 0)
                    {
                        sawTower = true;
                        FailIf(_player.Visible || _hud.Visible,
                            "The actual essence handoff must show the completed tower without Link or HUD.");
                    }
                    if (_dialogue.IsOpen && _dialogue.SourceTextId != 0x130b) _dialogue.Close();
                });
            FailIf(!sawTower || !_dialogue.IsOpen || _dialogue.SourceTextId != 0x130b ||
                postD7.Stage != PostD7Stage.Ascent || IsTransitioning || _player.Visible ||
                _hud.StatusBarHidden || !_hud.Visible || _sound.ActiveMusic != 0x21 || _sound.NativeActiveMusic != 0,
                "Rolling Sea must reach Ambi's TX_130b after invisible arrival $0f, retaining MUS_DISASTER $21 and the cleared music gate.");
            _dialogue.Close();
            LoadValidationRoom(5, 0x61); StepGameplayUpdates(3, Vector2.Zero);
            var collected = _entities.Entities<DungeonEssence>().Single();
            FailIf(!collected.Collected || collected.Visible, "Collected Rolling Sea must remain hidden on re-entry.");
            _player.WarpTo(new(104, 72)); StepGameplayUpdates(40, Vector2.Up, batched: batch);
            FailIf(_dialogue.IsOpen || _inventory.Essences != 0x7f, "Repeating the pedestal approach must not grant or replay Rolling Sea.");
        }
    }
}
