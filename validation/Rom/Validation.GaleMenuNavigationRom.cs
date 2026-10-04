using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGaleMenuNavigationRom() => RunGaleMenuNavigationRom(false);

    private void RunGaleMenuNavigationRom(bool compareOam, bool compareFramePixels = false)
    {
        int hostCase1 = 0;
        foreach (int group in new[] { 0, 1 })
        foreach (bool planted in new[] { false, true })
        foreach (int visitMask in new[] { 1, 0x2a, 0x55, 0xff })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(group, 0x33); _entities.Clear();
            // Independent source expectations: data/ages/treeWarps.s. The
            // present table skips its first entry until room $ac bit 7 is set.
            int[] destinations = group == 0
                ? planted ? [0xac, 0x13, 0x78, 0xc1] : [0x13, 0x78, 0xc1]
                : [0x08, 0x25, 0x2d, 0x78, 0x80, 0xc1];
            foreach (int room in new[] { 0xac, 0x13, 0x08, 0x25, 0x2d, 0x78, 0x80, 0xc1 })
                _saveData.SetRoomFlag(group, room, 0xff, false);
            if (planted) _saveData.SetRoomFlag(0, 0xac, 0x80);
            if (compareOam)
            {
                _saveData.WriteWramByte(0xc63e, (byte)group);
                _saveData.WriteWramByte(0xc63f, 0x45);
            }
            int[] visited = destinations.Where((room, index) => (visitMask & (1 << index)) != 0).ToArray();
            foreach (int room in visited) _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagVisited);
            var rom = new MenuRom(_saveData, _currentRoom);
            _player.BeginGale(); _mapMenu.OpenGale();
            StepGameplayUpdates(22, Vector2.Zero, [], [], batched);
            rom.OpenImmediately(5);
            // OpenImmediately supplies the white screen swap; retain the
            // eleven menu dispatches made by the application's fade-in.
            for (int frame = 0; frame < 11; frame++) rom.Update(0, 0, frame);
            rom.ClearSounds();
            FailIf(!_mapMenu.IsOpen || _mapScreen.CursorRoom != visited[0] || rom[0xcbb6] != visited[0],
                $"Gale ${group:x2} planted={planted} visits=${visitMask:x2}: first visited destination differs from treeWarps.s.");
            var sounds = _sound.AttachPlayRequestAudit();
            Vector2 position = _player.PrecisePosition;
            int edge = 0, update = 0, expected = 0;
            Texture2D? lastFrameBackground = null;
            byte[]? lastFrameCommands = null;
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                edge = pressed;
                StepGameplayUpdates(count, Vector2.Right, MenuRomActions(held ?? pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed, _saveData.ReadWramByte(0xc622)); edge = 0;
                    string context = $"Gale navigation ${group:x2} planted={planted} visits=${visitMask:x2} update={++update}";
                    FailIf(!_gameplayPause.IsLeased || !_mapMenu.IsOpen || _player.PrecisePosition != position,
                        context + ": menu ownership leaked gameplay input.");
                    FailIf(_mapMenu.GaleState != rom[0xcbcd] || _mapScreen.CursorRoom != rom[0xcbb6] ||
                        _mapScreen.PopupSize != rom[0xcbbd] || _mapScreen.PopupPrimary != rom[0xcbbe] ||
                        _mapScreen.PopupAlternate != rom[0xcbc0], context + $": cursor/popup runtime={_mapMenu.GaleState}/${_mapScreen.CursorRoom:x2}/{_mapScreen.PopupSize}/{_mapScreen.PopupPrimary}/{_mapScreen.PopupAlternate}, native={rom[0xcbcd]}/${rom[0xcbb6]:x2}/{rom[0xcbbd]}/{rom[0xcbbe]}/{rom[0xcbc0]}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": native navigation sounds differ.");
                    if (compareOam) CompareMapMenuOamRom(rom, context + $" batch={batched}");
                    if (compareFramePixels) CompareMapFramePixelsRom(rom, ref lastFrameBackground, ref lastFrameCommands,
                        context + $" frame batch={batched}");
                });
            }
            Step(55);
            foreach (int key in new[] { 0x10, 0x20, 0x40, 0x80, 0xf0 })
            for (int count = 0; count < 8; count++)
            {
                expected = (expected + (key is 0x20 or 0x40 ? visited.Length - 1 : 1)) % visited.Length;
                Step(1, key);
                FailIf(_mapScreen.CursorRoom != visited[expected], "Gale direction priority/wrapping did not follow the source's visited tree order.");
            }
            Step(60, held: 0x10);
            Step(8);
            _mapMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US Gale present/past tree ordering, planted scent-tree offset, sparse visits/single destination, all direction priorities/wrapping/autofire, popup animation/sounds and modal ownership through split/batched gameplay.");
    }
}
