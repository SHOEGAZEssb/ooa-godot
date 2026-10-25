using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceSimulatedMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool textPause in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MysterySeeds, 0x20);
            var record = new DekuForestPalaceEventDatabase().Record;
            _saveData.SetGlobalFlag(record.EntranceFlag, false); _saveData.SetGlobalFlag(record.CompletionFlag, false);
            LoadValidationRoom(1, record.EntranceRoom);
            var palace = _roomEvents.Get<DekuForestPalaceEvent>();
            // Declare a running input lane without other interaction scripts.
            // This comparison covers the actual event's Link handoff, not the
            // palace's complete multi-room guard/reward choreography.
            SomariaPrivate<CutsceneCommandRunner>(palace, "_singleRunner").Clear();
            SomariaPrivate<Dictionary<string, NpcCharacter>>(palace, "_actors").Clear();
            SomariaPrivate<List<NpcCharacter>>(palace, "_genericGuards").Clear();
            SomariaPrivate<List<PalaceSideGuardScriptHost>>(palace, "_sideGuards").Clear();
            _entities.Clear(); _inventory.SetScriptedEquippedItems(0, 0);
            byte floor = (from y in Enumerable.Range(0, 8) from x in Enumerable.Range(0, 10)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                let terrain = _currentRoom.GetTerrainInfo(point)
                where terrain.Type == TerrainType.Normal && terrain.Hazard == HazardType.None && !_currentRoom.IsSolid(point)
                select _currentRoom.GetMetatile(point)).First();
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), floor, 0, 0);
            _player.WarpTo(new(72.25f, 112.5f)); _player.Face(Vector2I.Up);
            F1Set(palace, "_stage", DekuForestPalaceStage.Entrance);
            F1Set(palace, "_playerInput", PalacePlayerInput.Enter); F1Set(palace, "_playerInputCounter", 8); F1Set(palace, "_entranceHorizontal", 1);
            _player.BeginCutsceneControl(owner: palace);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 72, 112) { NativeGameKeysEnabled = true };
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            native.Word(0xd00a, 0x7080); native.Word(0xd00c, 0x4840);
            // Original getSimulatedInput consumes this declared RAM stream:
            // eight up-right visits then sustained up. No lookup is mocked.
            native[0xcbc3] = 1; native.Word(0xcbc4, 0); native[0xcbc6] = 5; native.Word(0xcbc7, 0xc300);
            native.Word(0xc300, 8); native[0xc302] = 0x50; native.Word(0xc303, 0x40); native[0xc305] = 0x40;
            byte[] observed = new byte[4], expected = new byte[4], sentinel = [0xa5, 0x5a, 0x3c, 0xc3];
            var probe = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void SeedScratch()
            {
                for (int i = 0; i < 4; i++) { _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]); native[0xcec0 + i] = sentinel[i]; }
            }
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterLink: () =>
                {
                    for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                }); update++;
                FailIf(!observed.SequenceEqual(expected),
                    $"Palace simulated Link visit={update}, text={textPause}: movement must publish source scratch before ITEM/ENEMY/PART consumers, including masked visits.");
                FailIf(_player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f),
                    $"Palace simulated Link visit={update}: exact movement/text/control-mask handoff differs.");
                SeedScratch();
            });
            SeedScratch(); Step(4);
            if (textPause) { _dialogue.ShowMessage("Simulated input pause.", _player.Position.Y); native[0xcba0] = 1; }
            else { _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); native[0xcc8a] = 1; }
            Step(3);
            if (textPause) { _dialogue.Close(); native[0xcba0] = 0; }
            else { _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); native[0xcc8a] = 0; }
            Step(9);
            FailIf(SomariaPrivate<int>(palace, "_playerInputCounter") != 0,
                "Native simulated-input counters advance during text/Link masking even when Link does not move.");
            palace.Cancel(); _entities.Clear();
        }
    }
}
