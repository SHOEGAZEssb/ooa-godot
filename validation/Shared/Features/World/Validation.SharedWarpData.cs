
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedWarpData()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages,OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            string root = OracleAssetPack.ForGame(game);
            var data = WarpDatabase.For(root);
            var rom = ValidationRom.LoadCleanUs(game);
            int bank4 = 4 * 0x4000;
            int sourceTable = bank4 + (ages ? 0x359e : 0x3457);
            int sourceStart = bank4 + (ages ? 0x35ae : 0x3467);
            int destinationTable = bank4 + (ages ? 0x2f5b : 0x2d4e);
            int destinationStart = bank4 + (ages ? 0x2f6b : 0x2d5e);
            int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
            int dispatch = 5 * 0x4000 + (ages ? 0x0a26 : 0x09df) + 6;
            int[] pointers = ages ? [0x4a4c,0x4a59,0x4a9b,0x4aa5,0x4b68,0x4b79,0x4d89,0x4a4f,0x4c2d,0x4d19,0x4a4f,0x4d47,0x4a6c,0x4a4f,0x4a52,0x4d83] :
                [0x4a05,0x4a12,0x4a54,0x4a5e,0x4b21,0x4b32,0x4b83,0x4bde,0x4c55,0x4d41,0x4a08,0x4d6f,0x4a25,0x4a08,0x4a0b,0x4dae];
            WarpTransitionHandler[] handlers = [WarpTransitionHandler.Standing,WarpTransitionHandler.SetRespawn,WarpTransitionHandler.FadeOut,
                WarpTransitionHandler.Walk,WarpTransitionHandler.Instant,WarpTransitionHandler.Fall,
                ages ? WarpTransitionHandler.TimeWarp : WarpTransitionHandler.Trampoline,
                ages ? WarpTransitionHandler.KeepFacing : WarpTransitionHandler.HollyFall,
                WarpTransitionHandler.Subrosia,WarpTransitionHandler.Hole,WarpTransitionHandler.KeepFacing,WarpTransitionHandler.SlowFall,
                WarpTransitionHandler.ParameterFacing,WarpTransitionHandler.KeepFacing,WarpTransitionHandler.Shifted,WarpTransitionHandler.Invisible];
            for (int mode = 0; mode < 16; mode++)
                FailIf(Word(dispatch + mode * 2) != pointers[mode] || data.HandlerFor(mode) != handlers[mode],
                    $"{game}: mode 0x{mode:x2} must retain its native pointer/handler, including aliases and game-specific dispatch.");
            // Clean-US boundaries: last source group ends at $7ede/$7e03;
            // destination rows end immediately before warpSourcesTable.
            int sourceCount = ((bank4 + (ages ? 0x3ede : 0x3e03)) - sourceStart) / 4;
            int destinationCount = (sourceTable - destinationStart) / 3;
            var sourceRows = GeneratedTable.Load(root + "objects/warps.tsv",new("native source comparison",GeneratedTableKeySemantics.Unique,
                ["index","flags","key","data-low","data-high","pointer","source"],["index"],headerRequired:true));
            var destinationRows = GeneratedTable.Load(root + "objects/warp_destinations.tsv",new("native destination comparison",GeneratedTableKeySemantics.Unique,
                ["index","room","position","parameter","transition","source"],["index"],headerRequired:true));
            FailIf(sourceRows.Rows.Count != sourceCount || destinationRows.Rows.Count != destinationCount,
                $"{game}: warp import must retain the complete native source/destination extents.");
            for (int index = 0; index < sourceCount; index++)
            {
                var row = sourceRows.Rows[index]; int at = sourceStart + index * 4; int flags = rom.Span[at];
                int pointer = flags != 0xff && (flags & 0x40) != 0 ? (bank4 + (Word(at + 2) & 0x3fff) - sourceStart) / 4 : -1;
                FailIf(row.UnsignedDecimal(0) != index || row.HexByte(1) != flags || row.HexByte(2) != rom.Span[at + 1] ||
                    row.HexByte(3) != rom.Span[at + 2] || row.HexByte(4) != rom.Span[at + 3] || row.Decimal(5) != pointer,
                    $"{game}: native warp source row ${index:x4} changed flags/order/key/bytes/pointer, including unreachable or fallthrough rows.");
            }
            for (int index = 0; index < destinationCount; index++)
            {
                var row = destinationRows.Rows[index]; int at = destinationStart + index * 3;
                FailIf(row.HexByte(1) != rom.Span[at] || row.HexByte(2) != rom.Span[at + 1] ||
                    row.UnsignedDecimal(3) != rom.Span[at + 2] >> 4 || row.UnsignedDecimal(4) != (rom.Span[at + 2] & 15),
                    $"{game}: native warp destination ${index:x4} differs.");
            }
            var groups = GeneratedTable.Load(root + "objects/warp_groups.tsv",new("native warp root comparison",GeneratedTableKeySemantics.Unique,
                ["group","source-start","destination-start","end-marker","bias-group","bias-address","small-groups","small-edge-split","large-edge-split","source"],["group"],headerRequired:true));
            FailIf(groups.Rows.Count != 8,$"{game}: every native warp group must be imported.");
            for (int group = 0; group < 8; group++)
            {
                var row = groups.Rows[group];
                FailIf(row.UnsignedDecimal(0) != group || row.UnsignedDecimal(1) != (bank4 + (Word(sourceTable + group * 2) & 0x3fff) - sourceStart) / 4 ||
                    row.UnsignedDecimal(2) != (bank4 + (Word(destinationTable + group * 2) & 0x3fff) - destinationStart) / 3 ||
                    row.Boolean01(3) != ages || row.Decimal(4) != (ages ? -1 : 2) || row.HexWord(5) != (ages ? 0 : 0xc6df) ||
                    row.UnsignedDecimal(6) != 4 || row.HexByte(7) != (ages ? 0x58 : 0x60) || row.HexByte(8) != 0x80,
                    $"{game}: native group ${group:x1} lost a root/default/end/quadrant/Maku-growth contract.");
            }
            for (int mode = 0; mode < 6; mode++)
            {
                int facingTable = 5 * 0x4000 + (ages ? 0x0a88 : 0x0a41);
                int facingAt = 5 * 0x4000 + (Word(facingTable + mode * 2) & 0x3fff);
                var facing = new Dictionary<int,int>();
                while (rom.Span[facingAt] != 0) { facing.TryAdd(rom.Span[facingAt],rom.Span[facingAt + 1]); facingAt += 2; }
                for (int tile = 0; tile < 256; tile++)
                {
                    int direction = facing.TryGetValue(tile,out int foundDirection) ? foundDirection : 2;
                    Vector2I expectedFacing = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left };
                    FailIf(data.FacingDirection(mode,(byte)tile) != expectedFacing,
                        $"{game}: warp arrival facing must use native collision mode ${mode:x2}, tile ${tile:x2}, including default DIR_DOWN.");
                }
                int table = 0x4000 + (ages ? 0x2238 : 0x20e8);
                int at = 0x4000 + (Word(table + mode * 2) & 0x3fff);
                var expected = new Dictionary<int,byte>();
                while (rom.Span[at] != 0) { expected.TryAdd(rom.Span[at],rom.Span[at + 1]); at += 2; }
                for (int tile = 0; tile < 256; tile++)
                {
                    bool found = expected.TryGetValue(tile,out byte parameter);
                    bool actual = data.TryGetWarpTileParameter(mode,(byte)tile,out byte actualParameter);
                    FailIf(actual != found || actual && actualParameter != parameter,
                        $"{game}: warpTileTable mode ${mode:x2}, tile ${tile:x2} lost native classification/parameter (including Seasons' chimney).");
                }
            }
            var save = OracleSaveData.CreateStandardGame(game);
            var memory = OracleRuntimeState.CreateInitialized(game);
            _ = new InventoryState(TreasureDatabase.For(root),save,runtimeState:memory);
            var native = new WarpLookupRom(game);
            var nativeDestination = new WarpDestinationRom(game);
            var cases = new HashSet<(int Group,int Room,int Position)>();
            for (int group = 0; group < 8; group++)
            for (int room = 0; room < 256; room++)
            foreach (int position in new[] { 0,0x21,0x44,0xff }) cases.Add((group,room,position));
            // Add every pointed entry and its neighboring byte keys, without
            // using the generated stream to select native expectations.
            for (int group = 0; group < 8; group++)
            {
                int at = bank4 + (Word(sourceTable + group * 2) & 0x3fff);
                while ((rom.Span[at] & 0x80) == 0)
                {
                    if ((rom.Span[at] & 0x40) != 0)
                    {
                        int child = bank4 + (Word(at + 2) & 0x3fff);
                        for (int count = 0; count < sourceCount && rom.Span[child] != 0xff; count++,child += 4)
                        {
                            int key = rom.Span[child + 1];
                            foreach (int position in new[] { (key - 1) & 255,key,(key + 1) & 255 }) cases.Add((group,rom.Span[at + 1],position));
                            if ((rom.Span[child] & 0x80) != 0) break;
                        }
                    }
                    at += 4;
                }
            }
            foreach (var input in cases)
            {
                var reference = native.Lookup(input.Group,input.Room,input.Position);
                bool found = data.TryGetTileWarpSource(input.Group,input.Room,input.Position,out var warp);
                Compare(found,warp,reference,0,$"tile ${input.Group:x1}:${input.Room:x2}/${input.Position:x2}");
            }
            for (int group = 0; group < 8; group++)
            for (int room = 0; room < 256; room++)
            foreach (Vector2I direction in new[] { Vector2I.Up,Vector2I.Down })
            foreach (int x in new[] { -1,0x57,0x58,0x5f,0x60,0x7f,0x80,256 })
            {
                var reference = native.Lookup(group,room,0,direction,x);
                bool found = data.TryGetEdgeWarp(group,room,direction,new(x,0),out var warp);
                Compare(found,warp,reference,direction == Vector2I.Down ? 4 : 0,$"edge ${group:x1}:${room:x2}, direction={direction}, X={x}");
            }
            if (!ages)
            {
                foreach (byte growth in new byte[] { 0,1,3,6,9 })
                {
                    memory.SetWramByte(0xc6df,growth);
                    foreach (var input in cases)
                    {
                        var reference = native.Lookup(input.Group,input.Room,input.Position);
                        if (!reference.Found || reference.Group != 2) continue;
                        memory.SetWramByte(0xc6df,0x55);
                        bool found = data.TryGetTileWarpSource(input.Group,input.Room,input.Position,out var warp);
                        memory.SetWramByte(0xc6df,growth);
                        Compare(found,warp,reference,0,$"Maku growth ${growth:x2}, tile ${input.Group:x1}:${input.Room:x2}/${input.Position:x2}");
                    }
                }
            }

            void Compare(bool found, Warp warp, NativeWarpLookupResult reference, int sourceParameter, string context)
            {
                FailIf(found != reference.Found,$"{game}: native warp presence differs for {context}.");
                if (!found) return;
                FailIf(!warp.DestinationIsIndex || warp.DestinationRoom != reference.Index || warp.DestinationGroup != reference.Group,
                    $"{game}: native source lookup must retain the unresolved destination index for {context}.");
                nativeDestination.Apply(warp.SourceGroup,-1,reference.Group,reference.Index,0,ages ? 0 : memory.ReadWramByte(0xc6df));
                warp = data.ResolveDestination(warp,memory);
                FailIf(warp.DestinationIsIndex || warp.DestinationGroup != nativeDestination.Group || warp.DestinationRoom != nativeDestination.Room ||
                    new Vector2((warp.DestinationPosition & 15) * 16 + 8,(warp.DestinationPosition >> 4) * 16 + 8) != nativeDestination.Position ||
                    ((warp.DestinationParameter << 4) | warp.DestinationTransition | 0x80) != nativeDestination.Transition || warp.SourceTransition != (reference.Transition & 15) ||
                    warp.SourceParameter != sourceParameter,
                    $"{game}: native ordered/default/fallthrough/destination resolution differs for {context}.");
            }
        }
    }
}
