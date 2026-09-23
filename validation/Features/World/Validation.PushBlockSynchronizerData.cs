namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidatePushBlockSynchronizerData()
    {
        var data = new PushBlockSynchronizerDatabase();
        // Independent transcription: mainData.s places $bd:$00 immediately
        // after $21:$13/$14; neither object contains coordinate operands.
        foreach (int room in new[] { 0x9b,0x9e })
        {
            var records = data.GetRoomRecords(4,room);
            FailIf(records.Count != 1 || records[0].Order != 1 ||
                !records[0].Source.EndsWith($"group4Map{room:x2}ObjectData"),
                $"INTERAC $bd:$00 in 4:{room:x2} lost source order $01 or provenance.");
        }
        FailIf(data.GetRoomRecords(4,0xa5).Count != 0 || data.GetRoomRecords(5,0x9b).Count != 0,
            "INTERAC $bd must not be inferred from colored blocks or a matching room ID.");
        FailIf(data.ScanStart != 0xbf || data.ExcludedTile != 0xda,
            "INTERAC $bd scans from $bf and excludes Somaria tile $da.");
        byte[] expected = [0xf0,0x01,0x10,0xff];
        for (int i = 0; i < expected.Length; i++)
            FailIf(data.DestinationOffsets[i] != expected[i],
                "interactionCheckAdjacentTileIsSolid must retain packed-byte cardinal offsets.");
    }
}
