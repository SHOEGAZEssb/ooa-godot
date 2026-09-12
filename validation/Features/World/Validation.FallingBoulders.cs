using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFallingBoulderMotion()
    {
        LoadValidationRoom(1, 0x33);
        var data = new FallingBoulderDatabase();
        FailIf(data.Sprite != "spr_boulder" || data.Palette != 5 || data.TileBase != 0 ||
            data.SourceGrayscaleInverted || data.Radius != 4 || data.Damage != 4 ||
            !data.Delays.SequenceEqual([45, 90, 135, 180]),
            "PART $45 lost source graphics, radius $04, damage $04 or staggered delays.");
        string expectedAnimation = string.Join('|', Enumerable.Range(0, 5).Select(i =>
            $"4,0@8,0,{i * 4},0;8,8,{i * 4 + 2},0"));
        FailIf(data.Animation != expectedAnimation,
            "partAnimation5ba4f must retain five four-update frames and both OAM cells.");

        var visualRock = new FallingBoulder(0, new(80, 8), data, new OracleRandom(),
            _ => { }, _currentRoom, point => point + new Vector2(0, 16));
        try
        {
            for (int update = 0; update <= 45; update++) visualRock.UpdateFrame(update);
            for (int update = 0; update <= 20; update++)
            {
                using Image actual = visualRock.CurrentTexture.GetImage();
                ValidateFallingBoulderPixels(actual, update / 4 % 5);
                string exportDirectory = OS.GetEnvironment("OOA_BOULDER_VISUAL_AUDIT");
                if (update % 4 == 0 && exportDirectory.Length != 0)
                    actual.SavePng(System.IO.Path.Combine(exportDirectory, $"boulder-{update / 4 % 5}.png"));
                visualRock.UpdateFrame(46 + update);
            }
        }
        finally { visualRock.Free(); }

        // Independent bank3 SPEED_140 vectors for angles $0d..$13.
        (int Y, int X)[] vectors = [(266,177), (295,122), (313,62), (320,0),
            (313,-62), (295,-122), (266,-177)];
        foreach (int subId in Enumerable.Range(0, 4))
        foreach (int startY in new[] { 8, 0x48 })
        {
            var random = new OracleRandom();
            var sounds = new List<int>();
            var rock = new FallingBoulder(subId, new(0x88, startY), data, random, sounds.Add,
                _currentRoom, point => point + new Vector2(0, 16));
            try
            {
                rock.SetPartSlot(subId);
                rock.UpdateFrame(0);
                int originY = startY == 8 ? 0 : startY - 4;
                int y = originY << 8, x = 0x8800, z = 0, speedZ = 0;
                int counter = (subId + 1) * 45, angle = 0, state = 1;
                int rng1 = 0x37, rng2 = 0x0d, calls = 0, bounces = 0, resets = 0, rejects = 0;
                FailIf(rock.Position != new Vector2(0x88, originY) || rock.Visible || random.Calls != 0,
                    $"PART $45:${subId:x2} state 0 moved, became visible or consumed RNG.");
                for (int update = 1; update <= 1600; update++)
                {
                    bool bounce = false;
                    bool move = state == 2;
                    if (!move)
                    {
                        if (--counter == 0) { state = 2; bounce = true; }
                    }
                    else
                    {
                        z += speedZ;
                        if (z >= 0) { z = 0; bounce = true; }
                        else speedZ += 0x20;
                    }
                    if (bounce)
                    {
                        int value;
                        do
                        {
                            rng2 = (((rng2 << 8) | rng1) * 3 & 0xffff) >> 8;
                            rng1 = (rng1 + rng2) & 255;
                            value = rng1 & 7;
                            calls++;
                            if (value == 7) rejects++;
                        } while (value == 7);
                        angle = value + 0x0d;
                        speedZ = -0x1a0;
                        bounces++;
                    }
                    if (move)
                    {
                        y = (y + vectors[angle - 0x0d].Y) & 0xffff;
                        x = (x + vectors[angle - 0x0d].X) & 0xffff;
                        if ((y >> 8) >= 0x88)
                        {
                            state = 1; counter = 180; resets++;
                            y = (originY << 8) | (y & 255);
                            x = 0x8800 | (x & 255);
                        }
                    }
                    rock.UpdateFrame(update);
                    FailIf(rock.State != state || rock.Counter != counter || rock.Angle != angle ||
                        rock.FixedPosition.YFixed != y || rock.FixedPosition.XFixed != x ||
                        rock.ZFixed != z || rock.SpeedZ != speedZ || rock.Visible != (state == 2) ||
                        random.Calls != calls || sounds.Count != bounces || sounds.Any(sound => sound != 0xb3),
                        $"PART $45:${subId:x2}, Y=${startY:x2}, update {update}: source bounce/reset/RNG diverged.");
                    FailIf(rock.ShadowDrawn != (state == 2 && z < 0 && ((update ^ subId) & 1) != 0),
                        "PART $45 shadow lost Z, visibility or part-slot parity.");
                }
                FailIf(resets < 4 || rejects == 0, "PART $45 regression did not reach repeat resets and RNG rejection.");
            }
            finally { rock.Free(); }
        }
        GD.Print("Validated PART $45 source attributes/OAM, all delays, exact bouncing arithmetic, RNG rejection, shadows and retained reset fractions.");
    }

    private static void ValidateFallingBoulderPixels(Image image, int frame)
    {
        // Independent SHA-256 fixtures from the disassembly's spr_boulder.png,
        // decoded as (255 - grayscale) / 85 per spr_boulder.properties invert:false.
        // standardSpritePaletteData slot $05 supplies these original RGB5 colors.
        string[] hashes = [
            "046bbc5b92d6ba1946aac5a19780a9014973d4f6d6bffd79c560fdbac5914f83",
            "5849a521807685957c489194dc3e1cccd47fe8ece2282ce3669cf29de37f5c6e",
            "b6691fbf253adf3cbd3e6149e2aa31c08ea2c9c6baf87866e3f783d4b17ef330",
            "1b927612fc2d75f0df37417bb91c97a543867927faeafc5b217891b85138e254",
            "e65c4b3ba150e61cc890888cbbe9758ae52afed41f299718eacbc0620a2e0f7d"];
        // Image's RGBA8 storage truncates RGB5 * 255 / 31.
        uint[] colors = [0, 0xffb431ff, 0xde0000ff, 0x000000ff];
        FailIf(image.GetWidth() != 32 || image.GetHeight() != 32,
            "PART $45/$3b lost its centered 16x16 OAM pair in the 32x32 frame.");
        byte[] indices = new byte[256];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            Color actual = image.GetPixel(x, y);
            uint pixel = actual.ToRgba32();
            if (x is < 8 or >= 24 || y is < 8 or >= 24)
            {
                FailIf(actual.A != 0, "PART $45/$3b drew outside its source OAM bounds.");
                continue;
            }
            int index = actual.A == 0 ? 0 : Array.IndexOf(colors, pixel);
            FailIf(index < 0, $"PART $45/$3b frame {frame}: unexpected palette pixel ${pixel:x8}.");
            indices[(y - 8) * 16 + x - 8] = (byte)index;
        }
        string hash = Convert.ToHexString(SHA256.HashData(indices)).ToLowerInvariant();
        FailIf(hash != hashes[frame],
            $"PART $45/$3b frame {frame}: sprite colors/transparency differ from spr_boulder.properties invert:false ({hash}).");
    }

    private void ValidateFallingBoulderRooms()
    {
        (int Room, int[] SubIds, int[] Packed)[] rooms = [
            (0x23, [2,0,1,3], [0x41,0x44,0x45,0x48]),
            (0x33, [3,1,2,0], [0x02,0x04,0x06,0x08]),
            (0x43, [1,0,3,2], [0x01,0x03,0x05,0x07])];
        foreach (var entry in rooms)
        {
            LoadValidationRoom(1, entry.Room);
            var rocks = _entities.Entities<FallingBoulder>().ToArray();
            FailIf(!rocks.Select(r => r.SubId).SequenceEqual(entry.SubIds) ||
                !rocks.Select(r => r.Position).SequenceEqual(entry.Packed.Select(p =>
                    new Vector2((p & 15) * 16 + 8, (p >> 4) * 16 + 8))) ||
                !rocks.Select(r => r.PartSlot).SequenceEqual([0,1,2,3]),
                $"Room 1:${entry.Room:x2} lost source-ordered PART $45 placements/slots.");
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            try
            {
                // Actual application update path: initialization is separate from delay 1.
                base._Process(1.0 / 60);
                int calls = _entities.RandomCalls;
                base._Process(44.0 / 60);
                FailIf(rocks.Any(r => r.Visible) || _entities.RandomCalls != calls || sounds.Count != 0,
                    $"Room 1:${entry.Room:x2} rocks activated before delay $2d.");
                base._Process(1.0 / 60);
                FailIf(rocks.Count(r => r.Visible) != 1 || !rocks.Single(r => r.SubId == 0).Visible ||
                    !sounds.SequenceEqual([0xb3]), "PART $45 first activation must occur on update 46.");
                int[] counters = rocks.Select(r => r.Counter).ToArray();
                Func<bool> priorText = _entities.TextActiveSource;
                _entities.TextActiveSource = static () => true;
                try { _entities.Update(20.0 / 60, _player); }
                finally { _entities.TextActiveSource = priorText; }
                FailIf(!rocks.Select(r => r.Counter).SequenceEqual(counters) || sounds.Count != 1,
                    "PART $45 advanced during dialogue.");
            }
            finally { _entities.SoundRequested -= sounds.Add; }
        }

        // Compare the complete gameplay path with identical save/RNG starts.
        string? individual = null;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _player.ApplicationUpdateOwned = false;
            _dialogue.ApplicationUpdateOwned = false;
            _entities.GameButtonJustPressedSource = static () => false;
            ResetValidationInput();
            LoadValidationRoom(1, 0x33);
            _player.WarpTo(new Vector2(80, 112));
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            try
            {
                if (batched) base._Process(600.0 / 60);
                else for (int update = 0; update < 600; update++) base._Process(1.0 / 60);
                string trace = string.Join(';', _entities.Entities<FallingBoulder>().Select(r =>
                    $"{r.State},{r.Counter},{r.Angle},{r.FixedPosition},{r.ZFixed},{r.SpeedZ}")) +
                    $"/{_entities.RandomCalls}/{_player.HealthQuarters}/{_player.Position}/" + string.Join(',', sounds);
                if (!batched) individual = trace;
                else FailIf(trace != individual, $"PART $45 individual and batched gameplay diverged.\nIndividual: {individual}\nBatched: {trace}");
            }
            finally { _entities.SoundRequested -= sounds.Add; }
        }

        LoadValidationRoom(1, 0x43);
        _transitions.BeginScroll(_player, Vector2I.Up, 0x33);
        UpdateScrollingTransition(8.0 / 60);
        FailIf(_entities.Entities<FallingBoulder>().Any(r => r.State != 1 || r.Visible || r.Counter != (r.SubId + 1) * 45),
            "Room 1:$33 destination rocks advanced during scrolling preload.");
        for (int update = 0; update < 150 && _transitions.ScrollActive; update++)
            UpdateScrollingTransition(1.0 / 60);
        FailIf(_transitions.ScrollActive, "Falling-boulder room scroll did not finish.");
        base._Process(1.0 / 60);
        FailIf(_entities.Entities<FallingBoulder>().Any(r => r.State != 1 || r.Counter != (r.SubId + 1) * 45 - 1),
            "Room 1:$33 rocks did not resume their delay after scrolling.");
        LoadValidationRoom(1, 0x53);
        FailIf(_entities.Entities<FallingBoulder>().Count != 0, "PART $45 leaked into room 1:$53 through label fallthrough.");
        LoadValidationRoom(1, 0x33);
        FailIf(_entities.Entities<FallingBoulder>().Count != 4 || _entities.Entities<FallingBoulder>().Any(r => r.State != 0),
            "Room 1:$33 re-entry retained old boulder state.");
        GD.Print("Validated falling rocks in rooms 1:$23/$33/$43, gameplay timing/batching, dialogue freeze, scrolling, teardown and re-entry.");
    }

    private void ValidateFallingBoulderContact()
    {
        LoadValidationRoom(1, 0x33);
        var rock = new FallingBoulder(0, new(80, 0x48), new FallingBoulderDatabase(),
            new OracleRandom(), _ => { }, _currentRoom, point => point + new Vector2(0, 16));
        try
        {
            rock.UpdateFrame(0);
            // Direct collision boundary fixtures are deliberately separate from room reachability.
            foreach (int offset in new[] { -11, -10, 9, 10 })
            {
                var save = OracleSaveData.CreateStandardGame();
                var player = new Player();
                AddChild(player);
                try
                {
                    player.Initialize(new ValidationRingPlayerWorld(), new InventoryState(_treasures, save),
                        rock.Position + new Vector2(offset, 0), new OracleRandom());
                    int health = player.HealthQuarters;
                    rock.HandleLinkContact(player);
                    FailIf(player.HealthQuarters != health, "Hidden PART $45 damaged Link.");
                }
                finally { player.Free(); }
            }
            for (int update = 1; update <= 45; update++) rock.UpdateFrame(update);
            foreach (int offset in new[] { -11, -10, 9, 10 })
            {
                var player = new Player();
                AddChild(player);
                try
                {
                    player.Initialize(new ValidationRingPlayerWorld(),
                        new InventoryState(_treasures, OracleSaveData.CreateStandardGame()),
                        rock.Position + new Vector2(offset, 0), new OracleRandom());
                    int health = player.HealthQuarters;
                    rock.HandleLinkContact(player);
                    int damage = offset is -10 or 9 ? 4 : 0;
                    FailIf(player.HealthQuarters != health - damage, "PART $45 contact lost radius $04, damage $04 or asymmetric byte boundary.");
                    rock.HandleLinkContact(player);
                    FailIf(player.HealthQuarters != health - damage, "PART $45 ignored Link invincibility.");
                }
                finally { player.Free(); }
            }
            rock.UpdateFrame(46); // zh=$fe still overlaps grounded Link.
            for (int update = 47; update <= 53; update++) rock.UpdateFrame(update);
            var highPlayer = new Player();
            AddChild(highPlayer);
            try
            {
                highPlayer.Initialize(new ValidationRingPlayerWorld(),
                    new InventoryState(_treasures, OracleSaveData.CreateStandardGame()), rock.Position, new OracleRandom());
                int health = highPlayer.HealthQuarters;
                rock.HandleLinkContact(highPlayer);
                FailIf(rock.Z >= -7 || highPlayer.HealthQuarters != health,
                    "PART $45 airborne rock ignored the source Z collision window.");
            }
            finally { highPlayer.Free(); }
        }
        finally { rock.Free(); }
        GD.Print("Validated PART $45 activation, one-heart contact, byte collision edges, invincibility and Z separation.");
    }
}
