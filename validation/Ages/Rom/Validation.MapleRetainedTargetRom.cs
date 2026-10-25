using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMapleRetainedAttachedTargetRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var (maple, native) = MapleBodyBoundary();
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter");
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(80, 72), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.Visible = true;
            // Declare an effect$24 publication and stationary carrier. Actual
            // thrown-carrier collision/motion is covered by the attachment ROM
            // comparison; here $10 masks ITEM while companion/PART run live.
            drop.AttachToItem(() => new ItemDropCarrier(6, new(80, 72), 0));
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Collecting);
            F1Set(maple, "_vehicle", 1); F1Set(maple, "_precisePosition", new Vector2(80.25f, 72.5f)); maple.Position = new(80, 72);
            F1Set(maple, "_targetItem", drop); F1Set(maple, "_targetItemIndex", 13);
            F1Set(maple, "_angle", 8); F1Set(maple, "_targetAngle", 8); F1Set(maple, "_speedRaw", 0x14);
            F1Set(maple, "_zFixed", -0x1800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            native[0xd104] = 7; native[0xd128] = 1; native[0xd109] = native[0xd13d] = 8; native[0xd110] = 0x14; native[0xd13c] = 22;
            native.Word(0xd10a, 0x4880); native.Word(0xd10c, 0x5040); native.Word(0xd10e, -0x1800); native.Word(0xd114, 0x40);
            native[0xd12e] = 13; native[0xd118] = 0xd0; native[0xd119] = 0xc0;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = native[at + 3] = 13; native[at + 4] = 2;
            native[at + 11] = 72; native[at + 13] = 80; native[at + 0x24] = 0x94; native[at + 0x25] = native[at + 0x29] = 1;
            native[at + 0x26] = native[at + 0x27] = 6; native[at + 0x2a] = 0x97; native.Word(at + 0x16, 0xd700);
            native[0xd700] = 1; native[0xd701] = 6; native[0xd704] = 1; native[0xd70b] = 72; native[0xd70d] = 80;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x30); native[0xcc8a] = 0x30;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(drop.State != MapleDroppedItemState.Attached || native[at + 2] != 0x8d, "The declared publication must enter source state3 before the retained-target boundary.");
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x10); native[0xcc8a] = 0x10;
            int update = 0;
            StepGameplayUpdates(6, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                FailIf((int)maple.Stage != native[0xd104] || maple.MapleScore != native[0xd129] ||
                    SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                    (drop.ZFixed & 0xffff) != native.Word(at + 14) ||
                    SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                    $"Maple empty selector visit={update} must retain relatedObj2 and execute its subsequent collision/state4 writes, including an attached last target.");
            });
            FailIf(native[at + 2] != 0x8d || maple.MapleScore != 0, "Forced state4 does not clear the source subid high bit or grant Maple a score.");
            _entities.Clear();
        }
    }
}
