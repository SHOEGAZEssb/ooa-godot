using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleHeartPieceAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(40, 64));
            FillValidationRoom(0xa0, width: 10, height: 8);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 64)
                { HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay();
            var encounter = new MapleEncounterState(_runtimeState);
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(0), encounter, 0, new(80, 72), 0));
            // Declare a health0 PARTSTATUS_DEAD reward boundary. Collection
            // is remote and must retry checked INTERAC$60 creation, not overlap.
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); F1Set(drop, "_swordCollectionPending", true);
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x15; native[at + 4] = 2;
            native[at + 11] = 72; native[at + 13] = 80;
            FillInteractionAdmissionPool(native, 0);
            int update = 0, hearts = _inventory.HeartPieces;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                FailIf(drop.Finished != (native[at] == 0) || encounter.LinkScore != native[0xd12a] ||
                    _saveData.MapleState != native[0xc644] || _inventory.HeartPieces != native[0xc6ac],
                    $"Maple Heart Piece admission visit={update}: source lifetime/score/save/inventory boundary differs.");
                var treasures = _entities.Entities<GroundTreasurePickup>().Where(treasure => !treasure.Finished).ToList();
                int sourceCount = Enumerable.Range(2, 14).Count(slot => native[0xd040 + slot * 256] != 0 && native[0xd041 + slot * 256] == 0x60);
                FailIf(treasures.Count != sourceCount || _saveData.HasRoomFlag(0, 0x33, OracleSaveData.RoomFlagItem) || (native[0xc733] & 0x20) != 0,
                    $"Maple Heart Piece visit={update}: treasures={treasures.Count}/{sourceCount}, room flags=${_saveData.GetRoomFlags(0, 0x33):x2}/${native[0xc733]:x2}, native treasure state{native[0xd244]}.{native[0xd245]}.");
            });
            Step(3);
            FailIf(drop.Finished || encounter.LinkScore != 0xb4 || _inventory.HeartPieces != hearts || _dialogue.IsOpen,
                "Three rejected Heart Piece attempts must retain the PART, grant nothing, and preserve source repeated score additions ($3c each).");
            if (!cancel)
            {
                var filler = InteractionAdmissionPool.Single(pair => pair.Value == 2).Key;
                _entities.RetireEntity(filler);
                for (int offset = 0; offset < 0x40; offset++) native[0xd240 + offset] = 0;
                Step(1);
                FailIf(!drop.Finished || encounter.LinkScore != 0xf0 || (_saveData.MapleState & 0x80) == 0 || _inventory.HeartPieces != hearts,
                    "Successful source admission sets Maple bit7/deletes PART, but only the later treasure owner grants inventory.");
                Step(2);
                FailIf(_inventory.HeartPieces != hearts + 1 || !_dialogue.IsOpen,
                    $"The shared Heart Piece interaction must grant and present exactly once: hearts={_inventory.HeartPieces}/{native[0xc6ac]}, text={_dialogue.IsOpen}/${native[0xcba0]:x2}, native treasure state{native[0xd244]}.{native[0xd245]}, Link=({native[0xd00d]},{native[0xd00b]})/state{native[0xd004]}.");
                _dialogue.Close(); native[0xcba0] = 0;
                Step(3);
            }
            _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0 || cancel && _inventory.HeartPieces != hearts,
                "Room cancellation must release a rejected Maple Heart Piece without a reward.");
        }
    }
}
