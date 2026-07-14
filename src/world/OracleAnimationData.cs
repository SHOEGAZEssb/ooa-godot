using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class OracleAnimationData
{
    public readonly record struct Header(int Sheet, int DestinationTile, int TileCount, int SourceTile);
    public readonly record struct Frame(int Duration, int HeaderIndex);

    private readonly Header[] _headers;
    private readonly Dictionary<int, Frame[][]> _groups = new();
    private readonly Image[] _sheets = new Image[3];

    public OracleAnimationData()
    {
        _headers = LoadHeaders();
        LoadTracks();
        for (int sheet = 0; sheet < _sheets.Length; sheet++)
        {
            Texture2D texture = GD.Load<Texture2D>($"res://assets/oracle/gfx/gfx_animations_{sheet + 1}.png");
            _sheets[sheet] = texture.GetImage();
        }
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
        string source = FileAccess.GetFileAsString("res://assets/oracle/animations/headers.tsv");
        var headers = new List<Header>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 5 || int.Parse(columns[0]) != headers.Count)
                throw new InvalidOperationException($"Malformed animation header row: {line}");
            headers.Add(new Header(
                int.Parse(columns[1]), int.Parse(columns[2]),
                int.Parse(columns[3]), int.Parse(columns[4])));
        }
        if (headers.Count != 112)
            throw new InvalidOperationException($"Expected 112 animation headers, loaded {headers.Count}.");
        return headers.ToArray();
    }

    private void LoadTracks()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/animations/tracks.tsv");
        var tracksByGroup = new Dictionary<int, List<Frame[]>>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 3)
                throw new InvalidOperationException($"Malformed animation track row: {line}");

            int group = int.Parse(columns[0]);
            int track = int.Parse(columns[1]);
            if (!tracksByGroup.TryGetValue(group, out List<Frame[]>? tracks))
            {
                tracks = new List<Frame[]>();
                tracksByGroup.Add(group, tracks);
            }
            if (track != tracks.Count)
                throw new InvalidOperationException($"Animation group {group:x2} has nonsequential tracks.");

            string[] encodedFrames = columns[2].Split(',');
            var frames = new Frame[encodedFrames.Length];
            for (int index = 0; index < encodedFrames.Length; index++)
            {
                string[] pair = encodedFrames[index].Split(':');
                frames[index] = new Frame(int.Parse(pair[0]), int.Parse(pair[1]));
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
