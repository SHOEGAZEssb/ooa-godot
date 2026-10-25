using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePolsVoiceInstrumentGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone); _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            EnsureHarpAndSongs(); LoadValidationRoom(4, 0x65);
            _inventory.SetScriptedEquippedItems(equippedB: 0, equippedA: TreasureId.Harp); _inventory.SelectHarpSong(1);
            Vector2 point = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                .SelectMany(y => Enumerable.Range(1, _currentRoom.WidthInTiles - 2).Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(p => !_collision.Collides(p) && _entities.Entities<PolsVoiceCharacter>().All(e => p.DistanceTo(e.Position) > 32));
            _player.WarpTo(point); StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var voices = _entities.Entities<PolsVoiceCharacter>().ToArray();
            FailIf(voices.Length != 2 || voices.Any(e => e.InitializationPending || e.IsDead), "Room $4:$65 must initialize both actual Pols Voices before instrument input.");
            var native = new HarpRom(_saveData, _random.CaptureState(), 4, 0x65, _currentRoom.TilesetFlags, (int)point.X, (int)point.Y);
            var cpu = CompanionField<FrontendRom>(native, "_rom");
            native[0xcdd1] = 2;
            // Declared initialized receivers isolate the dispatch gate; this
            // comparison does not certify native Pols initialization/RNG.
            for (int index = 0; index < 2; index++)
            {
                int at = 0xd080 + index * 256;
                native[at] = 1; native[at + 1] = 0x23; native[at + 4] = 8;
                native[at + 6] = 30; native[at + 0x29] = 1;
                native[at + 11] = (byte)voices[index].Position.Y; native[at + 13] = (byte)voices[index].Position.X;
            }
            int updates = 0, release = 0;
            int[] counters = voices.Select(voice => voice.Counter).ToArray();
            void Compare()
            {
                native.Update(updates++ == 0 ? 1 : 0, 1, _entities.FrameCounter);
                cpu.Call(0x2ea5, 0x0d); // Actual updateEnemies, after the original instrument parent.
                cpu.Call(0x5e58, 0x11); // Death-puff phase retains the count until its terminal animation.
                int sourceAlive = Enumerable.Range(0, 2).Count(index => native[0xd080 + index * 256] != 0);
                FailIf(voices.Count(voice => !voice.IsDead) != sourceAlive ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != native[0xcc8a] || _entities.RoomEnemyCount != native[0xcdd1],
                    $"Pols Voice instrument dispatch differs at update {updates}: mask=${native[0xcc8a]:x2}, source alive={sourceAlive}.");
                if (native[0xcc8a] == 0x7e)
                    FailIf(sourceAlive != 2 || voices.Where((voice, index) => voice.Counter != counters[index] || voice.DiedFromInstrument).Any() ||
                        native[0xd086] != 30 || native[0xd186] != 30,
                        "The instrument handler cannot bypass the initialized enemy playback gate.");
                else if (release == 0) release = updates;
            }
            StepGameplayUpdates(8, Vector2.Zero, ["attack"], ["attack"], batched, Compare);
            if (cancel)
            {
                _player.ClearNativeItemParents();
                FailIf(_harp.IsPlaying || _player.IsUsingHarp || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 || voices.Any(v => v.IsDead),
                    "Canceling playback must release its own mask without an early instrument death.");
            }
            else
            {
                for (int wait = 0; _harp.IsPlaying && wait < 700; wait++)
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], [], batched, Compare);
                FailIf(_harp.IsPlaying || release == 0 || voices.Any(v => !v.DiedFromInstrument) || _entities.RoomEnemyCount != 2,
                    $"The retained instrument byte must kill both Pols Voices on the source parent-release enemy pass, once each: playing={_harp.IsPlaying}, release={release}, died={string.Join(',', voices.Select(v => v.DiedFromInstrument))}, roomCount={_entities.RoomEnemyCount}, source={native[0xd080]}/{native[0xd180]}.");
                StepGameplayUpdates(40, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(_entities.RoomEnemyCount != 0 || native[0xcdd1] != 0, "The completed source death puffs must release both counts without repetition.");
            }
            LoadValidationRoom(4, 0xa1);
            FailIf(_harp.IsPlaying, "Room cancellation must release the instrument parent.");
        }
        ReinitializeGameplayForValidation();
    }
}
