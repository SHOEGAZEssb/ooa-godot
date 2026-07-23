using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class OracleAnimationData
{

    private readonly Header[] _headers;
    private readonly Dictionary<int, Frame[][]> _groups = new();
    private readonly Image[] _sheets = new Image[3];

    public OracleAnimationData()
    {
        _headers = LoadHeaders();
        LoadTracks();
        for (int sheet = 0; sheet < _sheets.Length; sheet++)
            _sheets[sheet] = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/gfx_animations_{sheet + 1}.png");
        ValidateHeaderRanges();
    }

    public int[] GetActiveHeaders(int group, long tick)
    {
        if (group == 0xff || !_groups.TryGetValue(group, out Frame[][]? tracks))
            return Array.Empty<int>();

        // Animation entries are DMA writes into VRAM, not interchangeable
        // whole frames. Waterfalls in particular alternate writes to three
        // separate destination ranges; every earlier write remains active
        // until another header overwrites that range.
        var result = new List<int>();
        foreach (Frame[] track in tracks)
            AppendAppliedHeaders(track, tick, result);
        return result.ToArray();
    }

    public bool TryGetOverride(int[] activeHeaders, int destinationTile, out Image source, out int sourceTile)
    {
        // Later DMA writes take precedence where ranges overlap.
        for (int index = activeHeaders.Length - 1; index >= 0; index--)
        {
            int headerIndex = activeHeaders[index];
            Header header = _headers[headerIndex];
            int offset = destinationTile - header.DestinationTile;
            if (offset < 0 || offset >= header.TileCount)
                continue;

            source = _sheets[header.Sheet - 1];
            sourceTile = header.SourceTile + offset;
            return true;
        }

        source = null!;
        sourceTile = -1;
        return false;
    }

    private static void AppendAppliedHeaders(Frame[] frames, long tick, List<int> result)
    {
        int cycleLength = 0;
        foreach (Frame frame in frames)
            cycleLength += frame.Duration;

        int frameIndex = 0;
        int counter = frames[0].Duration;

        void ApplyCurrentFrame()
        {
            result.Add(frames[frameIndex].HeaderIndex);
            frameIndex = (frameIndex + 1) % frames.Length;
            counter = frames[frameIndex].Duration;
        }

        // initializeAnimations first performs one normal counter update, then
        // two forced updates. A normal update only emits the first header when
        // its initial duration is one; each forced update emits unconditionally.
        counter--;
        if (counter == 0)
            ApplyCurrentFrame();
        ApplyCurrentFrame();
        ApplyCurrentFrame();

        void AdvanceTicks(long count)
        {
            for (long elapsed = 0; elapsed < count; elapsed++)
            {
                counter--;
                if (counter == 0)
                    ApplyCurrentFrame();
            }
        }

        // Once one full cycle has executed, all destination ranges have a
        // persistent value and subsequent complete cycles can be skipped.
        long remaining = Math.Max(0, tick);
        long firstCycle = Math.Min(remaining, cycleLength);
        AdvanceTicks(firstCycle);
        remaining -= firstCycle;
        if (remaining > 0)
            AdvanceTicks(remaining % cycleLength);
    }

    private static Header[] LoadHeaders()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/animations/headers.tsv",
            new GeneratedTableSchema(
                "animation DMA headers",
                GeneratedTableKeySemantics.Unique,
                ["index", "sheet", "destination-tile", "tile-count", "source-tile"],
                ["index"],
                headerRequired: true));
        var headers = new List<Header>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.UnsignedDecimal(0) != headers.Count)
                throw row.Invalid(0, $"sequential index {headers.Count}");
            headers.Add(new Header(
                row.UnsignedDecimal(1), row.UnsignedDecimal(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4)));
        }
        if (headers.Count != 112)
            throw new InvalidOperationException($"Expected 112 animation headers, loaded {headers.Count}.");
        return headers.ToArray();
    }

    private void LoadTracks()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/animations/tracks.tsv",
            new GeneratedTableSchema(
                "animation tracks",
                GeneratedTableKeySemantics.Grouped,
                ["group", "track", "frames(duration:gfx-index)"],
                ["group"],
                headerRequired: true));
        var tracksByGroup = new Dictionary<int, List<Frame[]>>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int group = row.UnsignedDecimal(0);
            int track = row.UnsignedDecimal(1);
            if (!tracksByGroup.TryGetValue(group, out List<Frame[]>? tracks))
            {
                tracks = new List<Frame[]>();
                tracksByGroup.Add(group, tracks);
            }
            if (track != tracks.Count)
                throw new InvalidOperationException($"Animation group {group:x2} has nonsequential tracks.");

            string[] encodedFrames = row.RequiredString(2).Split(',');
            var frames = new Frame[encodedFrames.Length];
            for (int index = 0; index < encodedFrames.Length; index++)
            {
                string[] pair = encodedFrames[index].Split(':');
                if (pair.Length != 2 ||
                    !int.TryParse(pair[0], out int duration) || duration <= 0 ||
                    !int.TryParse(pair[1], out int header) ||
                    header < 0 || header >= _headers.Length)
                {
                    throw row.Invalid(2, "comma-separated positive duration:header-index pairs");
                }
                frames[index] = new Frame(duration, header);
            }
            tracks.Add(frames);
        }

        foreach ((int group, List<Frame[]> tracks) in tracksByGroup)
            _groups[group] = tracks.ToArray();
        if (_groups.Count != 22)
            throw new InvalidOperationException($"Expected 22 animation groups, loaded {_groups.Count}.");
    }

    private void ValidateHeaderRanges()
    {
        foreach (Header header in _headers)
        {
            if (header.Sheet < 1 || header.Sheet > _sheets.Length ||
                header.DestinationTile < 0 || header.DestinationTile + header.TileCount > 256)
                throw new InvalidOperationException($"Animation header has an invalid destination: {header}.");

            Image sheet = _sheets[header.Sheet - 1];
            int availableTiles = (sheet.GetWidth() / 8) * (sheet.GetHeight() / 8);
            if (header.SourceTile < 0 || header.SourceTile + header.TileCount > availableTiles)
                throw new InvalidOperationException($"Animation header exceeds its source sheet: {header}.");
        }
    }
}

public readonly record struct Header(int Sheet, int DestinationTile, int TileCount, int SourceTile);

public readonly record struct Frame(int Duration, int HeaderIndex);
