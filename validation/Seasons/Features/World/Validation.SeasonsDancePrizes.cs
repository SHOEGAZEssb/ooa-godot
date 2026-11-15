namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsDancePrizes()
    {
        foreach (var prize in new[] { (Wins:0, Kind:0), (Wins:1, Kind:1), (Wins:1, Kind:2),
            (Wins:5, Kind:3), (Wins:5, Kind:4), (Wins:5, Kind:5), (Wins:8, Kind:6), (Wins:8, Kind:7), (Wins:255, Kind:1) })
        {
            var root = StartSeasonsAfterImpa(false);
            try
            {
                var s = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                s.DebugTravel(3, 0x95); app.Step(2, Vector2.Zero);
                s.Link.WarpTo(new(72, 48)); s.Link.Face(Vector2I.Down);
                s.Save.WriteField("wNumTimesPlayedSubrosianDance", (byte)prize.Wins);
                if (prize.Kind == 4) s.Save.WriteField("wRickyState", 0x20);
                if (prize.Kind == 5) { s.Save.SetLinkedGame(true); s.Memory.SetField("wIsLinkedGame", 1); }
                if (prize.Kind == 7) foreach (int ring in new[] { 0x3e, 0x3d, 0x1f, 0x1a }) s.Inventory.GrantAppraisedRingForDebug(ring);
                // Stage the documented post-win fade boundary; gameplay still owns script/treasure dispatch.
                var leader = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
                leader.WriteByte(0x44, 5); leader.WriteByte(0x45, 2);
                var seed = s.Random.CaptureState();
                var probe = new OracleRandom();
                for (int candidate = 0; candidate < 256; candidate++)
                {
                    var next = seed with { Rng1 = (byte)candidate, Rng2 = 0x0d };
                    probe.RestoreState(next);
                    bool gasha = probe.Next().Value < 0x60;
                    if (gasha != (prize.Kind == 2)) continue;
                    s.Random.RestoreState(next); break;
                }
                int text = prize.Kind switch { 0 => 0x0022, 3 => 0x003b, 2 or 7 => 0x004b, 6 => 0x0054, _ => 0x006b };
                int ticks = 0;
                while (!s.Dialogue.IsOpen && ticks++ < 100) app.Step(1, Vector2.Zero);
                FailIf(!s.Dialogue.IsOpen || s.Dialogue.SourceTextId != text ||
                    s.Save.ReadField("wNumTimesPlayedSubrosianDance") != Math.Min(255, prize.Wins + 1),
                    $"Dance wins{prize.Wins}/branch{prize.Kind} must select TX_{text:x4} and saturate its persistent counter; got TX_{s.Dialogue.SourceTextId:x4}.");
                FailIf(prize.Kind == 0 && s.Inventory.BoomerangLevel != 1 ||
                    prize.Kind == 3 && (!s.Inventory.HasTreasure(TreasureId.Flute) || s.Save.ReadField("wAnimalCompanion") != 0x0c) ||
                    prize.Kind == 6 && (s.Inventory.UnappraisedRingCount != 1 || !new[] { 0x7e, 0x7d, 0x5f, 0x5a }.Contains(s.Inventory.UnappraisedRingAt(0))),
                    "Native dance rewards must grant the boomerang, Strange Flute/Dimitri binding, or one unappraised prize ring.");
                var granted = (s.Inventory.BoomerangLevel, s.Inventory.GashaSeeds, s.Inventory.OreChunks,
                    s.Inventory.UnappraisedRingCount, s.Inventory.AnimalCompanion, s.Save.ReadField("wNumTimesPlayedSubrosianDance"));
                app.Step(4, Vector2.Zero, batched: true);
                FailIf(granted != (s.Inventory.BoomerangLevel, s.Inventory.GashaSeeds, s.Inventory.OreChunks,
                    s.Inventory.UnappraisedRingCount, s.Inventory.AnimalCompanion, s.Save.ReadField("wNumTimesPlayedSubrosianDance")),
                    "Holding dance reward text must not grant the prize or increment wins a second time.");
                s.Dialogue.Close(); app.Step(5, Vector2.Zero);
                s.DebugTravel(1, 0x24); app.Step(2, Vector2.Zero);
                FailIf(s.DisabledObjects != 0 || s.Memory.ReadField("wMenuDisabled") != 0 ||
                    s.Memory.ReadField("wDisableLinkCollisionsAndMenu") != 0,
                    "Cancelling a dance prize must release its native object/menu/collision writes.");
            }
            finally { root.Free(); }
        }
    }
}
