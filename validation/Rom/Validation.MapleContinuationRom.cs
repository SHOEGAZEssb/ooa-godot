using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleBombReleaseRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int bombZ in new[] { 0, -0x7c0 })
        {
            var (maple, native) = MapleBodyBoundary();
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter"); encounter.ObjectsDisabled = true;
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Collecting);
            F1Set(maple, "_vehicle", 1); F1Set(maple, "_precisePosition", new Vector2(80, 72)); maple.Position = new(80, 72);
            F1Set(maple, "_zFixed", -0x1800);
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(80, 72), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.BeginMapleCollection(1, () => maple.BookTargetPosition);
            F1Set(drop, "_state", MapleDroppedItemState.MapleRising); F1Set(drop, "_zFixed", -0x500); F1Set(drop, "_speedZ", -0x40);
            F1Set(drop, "_angle", 8); drop.Visible = true;
            F1Set(maple, "_targetItem", drop); F1Set(maple, "_targetItemIndex", 13);
            var bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, new BombDatabase().Data, 1, _ => { }));
            F1Set(bomb, "_setupPending", false); F1Set(bomb, "_state", BombState.MaplePulling);
            F1Set(bomb, "_precisePosition", new Vector2(80, 72)); bomb.Position = new(80, 72); F1Set(bomb, "_zFixed", bombZ);
            native[0xd104] = 7; native[0xd128] = 1; native[0xd10b] = 72; native[0xd10d] = 80; native.Word(0xd10e, -0x1800);
            native[0xd12e] = 13; native[0xd118] = 0xd0; native[0xd119] = 0xc0;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = 13; native[at + 4] = 4; native[at + 5] = 2;
            native[at + 0x29] = 1; native[at + 9] = 8; native[at + 0x10] = 0x14;
            native[at + 11] = 72; native[at + 13] = 80; native.Word(at + 14, -0x500); native.Word(at + 0x14, -0x40);
            int bombAt = _entities.DynamicItemSlotOf(_entities.EntityAdapters<BombRoomEntity>().Single()) * 256;
            native[bombAt] = 1; native[bombAt + 1] = 3; native[bombAt + 4] = 1;
            native[bombAt + 0x2f] = 0x80; native[bombAt + 11] = 72; native[bombAt + 13] = 80; native.Word(bombAt + 14, bombZ);
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                FailIf((int)maple.Stage != native[0xd104] || maple.MapleScore != native[0xd129] || drop.Finished != (native[at] == 0),
                    $"Maple bomb handoff z={bombZ}, visit={update}: port {maple.Stage}/score{maple.MapleScore}/finished{drop.Finished}; source ${native[0xd104]:x2}/score{native[0xd129]}/enabled{native[at]}.");
                if (!drop.Finished)
                    FailIf((drop.ZFixed & 0xffff) != native.Word(at + 14) ||
                        (SomariaPrivate<int>(drop, "_speedZ") & 0xffff) != native.Word(at + 0x14) ||
                        SomariaPrivate<int>(drop, "_speedRaw") != native[at + 0x10] || SomariaPrivate<int>(drop, "_angle") != native[at + 9],
                        $"Bomb interruption visit={update}: port Z${drop.ZFixed & 0xffff:x4}/v${SomariaPrivate<int>(drop, "_speedZ") & 0xffff:x4}/speed${SomariaPrivate<int>(drop, "_speedRaw"):x2}/angle${SomariaPrivate<int>(drop, "_angle"):x2}; source Z${native.Word(at + 14):x4}/v${native.Word(at + 0x14):x4}/speed${native[at + 0x10]:x2}/angle${native[at + 9]:x2}.");
            });
            for (int wait = 0; maple.Stage != MapleEncounterStage.BombStun && wait < 40; wait++) Step(batched ? 2 : 1);
            FailIf(maple.Stage != MapleEncounterStage.BombStun, "Declared vacuum bomb must reach its source stun boundary.");
            Step(3); _entities.Clear();
        }
    }

    private void ValidateMapleNoTargetContinuationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int angle in new[] { 0, 8, 0x10, 0x18 })
        {
            var (maple, native) = MapleBodyBoundary();
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Racing);
            F1Set(maple, "_precisePosition", new Vector2(80.25f, 72.5f)); maple.Position = new(80, 72);
            F1Set(maple, "_angle", angle); F1Set(maple, "_targetAngle", angle); F1Set(maple, "_speedRaw", 0x50);
            F1Set(maple, "_zFixed", -0x800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            native[0xd104] = 6; native[0xd109] = native[0xd13d] = (byte)angle; native[0xd110] = 0x50;
            native.Word(0xd10a, 0x4880); native.Word(0xd10c, 0x5040);
            native.Word(0xd10e, -0x800); native.Word(0xd114, 0x40); native[0xd13c] = 22;
            // Empty PART stream and initial relatedObj2=$0000 are declared.
            // The original state6 still moves after selecting state9.
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                FailIf(maple.Finished != (native[0xd100] == 0), "Maple's outcome departure deletion visit must match source.");
                if (maple.Finished) return;
                FailIf((int)maple.Stage != native[0xd104] || maple.Substate != native[0xd105] ||
                    SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f),
                    "Maple state6 must finish its movement on the visit that selects the empty-stream outcome.");
            });
            Step(2);
            _dialogue.Close(); native[0xcba0] = 0;
            Step(1);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wTextIsActive) != 0x80 || native[0xcba0] != 0x80 ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0x1f || native[0xcc8a] != 0x1f ||
                !_entities.IsTextActive() || _dialogue.IsOpen,
                "Outcome departure must hold native text activity without a textbox and publish exact mask$1f to the shared dispatcher.");
            for (int wait = 0; !maple.Finished && wait < 64; wait++) Step(batched ? 2 : 1);
            FailIf(!maple.Finished, "The held text signal must permit Maple's companion departure through deletion.");
            foreach (int address in new[] { 0xcba0, 0xcc02, 0xcc8a, 0xcc91 })
                FailIf(_runtimeState.ReadWramByte(address) != native[address] || native[address] != 0,
                    $"Maple completion must release shared control byte ${address:x4}.");
            _entities.Clear();
        }
    }

    private void ValidateMaplePartRetirementPhaseRom()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var (maple, native) = MapleBodyBoundary();
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter"); encounter.ObjectsDisabled = true;
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(80, 72), 0));
            F1Set(drop, "_state", MapleDroppedItemState.MapleReady);
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Collecting);
            F1Set(maple, "_vehicle", 2); F1Set(maple, "_targetItem", drop); F1Set(maple, "_targetItemIndex", 13);
            native[0xcc8a] = 1; native[0xd104] = 7; native[0xd128] = 2; native[0xd125] = 1;
            native[0xd12e] = 13; native[0xd118] = 0xd0; native[0xd119] = 0xc0;
            native[0xd0c0] = 1; native[0xd0c1] = 0x14; native[0xd0c2] = 13; native[0xd0c4] = 4; native[0xd0c5] = 3;
            native[0xd0e9] = 1;
            F1Fill(0, native);
            // Declared stationary grounded fillers, independently represented
            // by safe native state2 handlers rather than unknown part IDs.
            for (int slot = 1; slot < 16; slot++)
            {
                int at = 0xd0c0 + slot * 256; native[at + 1] = 0x14; native[at + 2] = 13;
                native[at + 4] = 2; native[at + 11] = 112; native[at + 13] = 120;
            }
            bool? availableBeforeParts = null;
            var probe = new PartMovementScratchProbe(() => availableBeforeParts = _entities.PartSlotAvailable);
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, beforeParts: () =>
                {
                    bool sourceAvailable = Enumerable.Range(0, 16).Any(slot => native[0xd0c0 + slot * 256] == 0);
                    FailIf(availableBeforeParts != sourceAvailable || sourceAvailable,
                        "A collected Maple item must occupy its physical slot through ITEM/ENEMY phases until its own PART deletion visit.");
                });
                FailIf(!drop.Finished || native[0xd0c0] != 0 || !_entities.PartSlotAvailable || maple.MapleScore != native[0xd129],
                    $"The later PART pass must consume the retirement signal and free the slot exactly once: finished={drop.Finished}, native enabled={native[0xd0c0]}, free={_entities.PartSlotAvailable}, score={maple.MapleScore}/{native[0xd129]}.");
            });
            _entities.Clear();
        }
    }
}
