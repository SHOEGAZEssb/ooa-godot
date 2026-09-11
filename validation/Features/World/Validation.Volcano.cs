using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVolcanoRumblePauses()
    {
        LoadValidationRoom(1, 2);
        var data = new VolcanoDatabase();
        _saveData.SetGlobalFlag(0x29, false);
        foreach (int initialFrame in new[] { 0, 1 })
        {
            var random = new OracleRandom();
            var expectedRandom = new OracleRandom();
            var sound = new OracleSoundEngine(new OracleSoundData(), enableOutput: false);
            var requests = new List<int>();
            var spawns = new List<RoomEntitySpawn>();
            int shakeY = 0, shakeX = 0;
            var ambient = new VolcanoRoomEntity(new(1, 2, 0, 5, 0, 0), data, _currentRoom,
                _saveData, random, id => { requests.Add(id); sound.PlaySound(id); },
                (y, x, _) => { shakeY = y; shakeX = x; },
                () => shakeY != 0 || shakeX != 0, _ => { }, () => true);
            try
            {
                ambient.UpdateFrame(new(_player, initialFrame, false), spawns);
                int expectedState = 1 + initialFrame;
                int expectedCounter = (expectedRandom.Next().Value & 0x7f) + 0x20;
                int quietWindows = 0, silentUpdates = 0, lastRumbleUpdate = -1000;
                FailIf(!requests.SequenceEqual([data.Constant("stop-sfx")]),
                    "$dc:$05 entry must stop any preceding eruption sound.");
                for (int update = 1; update <= 1800; update++)
                {
                    int frame = (initialFrame + update) & 0xff;
                    bool trembling = expectedState == 2;
                    bool rumbleDue = trembling && (frame & 15) == 0;
                    if ((frame & 1) == 0 && --expectedCounter == 0)
                    {
                        if (trembling) quietWindows++;
                        expectedState = trembling ? 1 : 2;
                        expectedCounter = (expectedRandom.Next().Value & 0x7f) + 0x20;
                    }
                    requests.Clear();
                    ambient.UpdateFrame(new(_player, frame, false), spawns);
                    sound.Tick();
                    FailIf(ambient.State != expectedState || ambient.Counter != expectedCounter ||
                        random.Calls != expectedRandom.Calls ||
                        shakeY != (trembling ? 8 : 0) || shakeX != shakeY ||
                        !requests.SequenceEqual(rumbleDue ? [data.Constant("rumble")] : Array.Empty<int>()),
                        $"$dc:$05 update {update}, entry parity {initialFrame}: tremor/quiet cadence, RNG or SND_RUMBLE $b3 diverged.");
                    if (rumbleDue) lastRumbleUpdate = update;
                    // sndRumbleChannel2 ends after 53 updates; channel 7's
                    // note lasts 60. cmdff then releases both SFX channels.
                    if (!trembling && update - lastRumbleUpdate > 60)
                    {
                        FailIf(sound.Channel(2).Active || sound.Channel(7).Active ||
                            sound.Apu.Voice(0).Volume != 0 || sound.Apu.Voice(3).Volume != 0,
                            "$dc:$05 quiet period retained audible SND_RUMBLE after its 60-update tail.");
                        silentUpdates++;
                    }
                }
                FailIf(quietWindows < 3 || silentUpdates < 100,
                    "$dc:$05 did not produce repeated, genuinely silent pauses over 30 seconds.");
                // Cancellation stops new requests. The existing one-shot must
                // finish naturally even when there is no next $dc:$05 entry.
                sound.PlaySound(data.Constant("rumble"));
                for (int update = 0; update < 61; update++) sound.Tick();
                FailIf(sound.Channel(2).Active || sound.Channel(7).Active,
                    "SND_RUMBLE $b3 did not finish after leaving its requesting room.");
            }
            finally { ambient.Node.Free(); sound.Free(); }
        }
        GD.Print("Validated Symmetry street tremor/quiet cycles, both entry parities, fresh pause RNG, SND_RUMBLE channel termination and exit tails.");
    }

    private void ValidateVolcanoEruption()
    {
        var data = new VolcanoDatabase();
        VolcanoStep[] expected = [new(0,30,0,255), new(30,0,0,255), new(180,180,15,8),
            new(60,60,31,16), new(30,0,0,255), new(0,120,0,255), new(15,15,0,255)];
        FailIf(!data.Steps.SequenceEqual(expected) || data.Constant("restored-flag") != 0x29,
            "volcanoHandler.s:@script or GLOBALFLAG_TUNI_NUT_PLACED $29 changed.");
        FailIf(data.Placements(1, 3).Select(r => (r.Order, r.SubId)).ToArray() is not [(0, 0x13), (1, 0x06)] ||
            data.Placements(0, 3).Count != 0,
            "Symmetry $1:$03 lost ordered $dc:$13/$06 or leaked eruption into the present.");
        foreach (int room in new[] { 2, 4, 0x12, 0x13, 0x14 })
            FailIf(data.Placements(1, room).Count != 1 || data.Placements(1, room)[0].SubId != 5,
                $"Symmetry $1:${room:x2} lost its aliased $dc:$05 shaking controller.");

        _saveData.SetGlobalFlag(0x29, false);
        LoadValidationRoom(1, 3);
        using Image before = _currentRoom.Texture.GetImage();
        ulong pixels = OracleGraphicsCache.PixelHash(before);
        var offsets = new List<Vector2>();
        _entities.ScreenShakeChanged += offsets.Add;
        void Tick() => _entities.Update(1.0 / 60, _player);
        Tick();
        using Image after = _currentRoom.Texture.GetImage();
        FailIf(OracleGraphicsCache.PixelHash(after) != pixels ||
            _currentRoom.GetMetatile(new(72, 24)) != 0xe4 ||
            _currentRoom.GetTerrainInfo(new(72, 24)).Hazard != HazardType.Lava,
            "$dc:$13 must write lava $e4 at $14 while preserving the rendered waterfall.");
        FailIf(_entities.ScreenShakeCounter != 0 || _entities.HorizontalScreenShakeCounter != 0,
            "$b2 state 0 advanced the shake script on its initialization update.");
        Tick();
        FailIf(_entities.ScreenShakeCounter != 0 || _entities.HorizontalScreenShakeCounter != 29 ||
            offsets.Last().Y != 0 || Math.Abs(offsets.Last().X) != 1,
            "$b2 first script step must shake X only with magnitude 1 for 30 updates.");
        for (int tick = 3; tick <= 31; tick++) Tick();
        Tick();
        FailIf(_entities.ScreenShakeCounter != 29 || _entities.HorizontalScreenShakeCounter != 0,
            "$b2 second script step did not begin exactly on update 32.");
        for (int tick = 33; tick <= 61; tick++) Tick();
        Tick();
        FailIf(_entities.ScreenShakeCounter != 179 || _entities.HorizontalScreenShakeCounter != 179,
            "$b2 eruption must begin on update 62, after the two 30-update tremors.");
        for (int tick = 0; tick < 25; tick++) Tick();
        FailIf(_entities.Entities<VolcanoRock>().Count == 0,
            "Symmetry $1:$03 eruption did not spawn PART_VOLCANO_ROCK $11:$01.");
        _entities.ScreenShakeChanged -= offsets.Add;

        LoadValidationRoom(1, 0x13);
        FailIf(_entities.Entities<VolcanoRock>().Count != 0 || _entities.ScreenShakeCounter != 0,
            "Leaving the volcano retained room-local rocks or screen shake.");
        _saveData.SetGlobalFlag(0x29);
        LoadValidationRoom(1, 3);
        for (int tick = 0; tick < 100; tick++) Tick();
        FailIf(_entities.Entities<VolcanoRock>().Count != 0 || _entities.ScreenShakeCounter != 0 ||
            _entities.HorizontalScreenShakeCounter != 0,
            "Restoring the Tuni Nut did not suppress $dc:$06 on room re-entry.");
        _saveData.SetGlobalFlag(0x29, false);
        LoadValidationRoom(1, 0x13);
        _transitions.BeginScroll(_player, Vector2I.Up, 3);
        UpdateScrollingTransition(8.0 / 60);
        FailIf(_entities.Entities<VolcanoRock>().Count != 0 || _entities.HorizontalScreenShakeCounter != 0,
            "Incoming $b2 advanced its eruption during scrolling.");
        for (int i = 0; i < 100 && _transitions.ScrollActive; i++) UpdateScrollingTransition(1.0 / 60);
        FailIf(_transitions.ScrollActive, "Symmetry volcano scroll did not finish.");
        for (int tick = 0; tick < 90; tick++) Tick();
        FailIf(_entities.Entities<VolcanoRock>().Count == 0,
            "Scroll-preloaded $b2 failed to start after destination activation.");
        GD.Print("Validated Symmetry eruption entry, exact shake boundaries, lava collision, scrolling, cancellation and Tuni Nut suppression.");
    }

    private void ValidateVolcanoRockLifecycle()
    {
        LoadValidationRoom(1, 3);
        var data = new VolcanoDatabase();
        var random = new OracleRandom();
        var expectedRandom = new OracleRandom();
        int angle = expectedRandom.Next().Value & 31;
        int landing = expectedRandom.Next().Value;
        var sounds = new List<int>();
        var spawns = new List<RoomEntitySpawn>();
        var rock = new VolcanoRock(new(80, 24), data, _currentRoom, random, sounds.Add,
            point => point + new Vector2(0, 16));
        try
        {
            void Tick() => rock.UpdateFrame(new(_player, 0, false), spawns);
            Tick();
            FailIf(rock.State != 1 || rock.Angle != angle || rock.SpeedZ != -0x400 || random.Calls != 1 || !rock.Visible,
                "$11:$01 state 0 lost its random angle, -$0400 launch or visible animation $01.");
            using Image launchImage = rock.CurrentTexture.GetImage();
            ulong launchPixels = OracleGraphicsCache.PixelHash(launchImage);
            int z = 0, speed = -0x400;
            while ((((24 + (z >> 8) + 8) & 255)) < 0xf8)
            {
                z += speed; speed += 0x10;
                Tick();
                FailIf(rock.Z != (z >> 8) || rock.Position != new Vector2(80, 24),
                    "$11:$01 launch changed fixed-point gravity or incorrectly applied XY speed.");
            }
            Tick();
            Vector2 target = new(((landing & 7) + 1) * 16 + 8, (landing & 0x70) + 8);
            FailIf(rock.State != 2 || rock.Counter != 30 || rock.Visible || rock.Position != target ||
                rock.Z != -(int)target.Y || random.Calls != 2,
                "volcanoRock_setRandomPosition lost its single correlated RNG draw or offscreen delay.");
            using Image fallingImage = rock.CurrentTexture.GetImage();
            FailIf(OracleGraphicsCache.PixelHash(fallingImage) == launchPixels,
                "$11:$01 did not select its imported falling OAM after launching.");
            for (int i = 0; i < 29; i++) Tick();
            FailIf(rock.Visible || rock.State != 2 || rock.Counter != 1, "$11:$01 revealed before delay update 30.");
            Tick();
            FailIf(!rock.Visible || rock.State != 3 || rock.Counter != 16, "$11:$01 delay zero did not reveal falling animation $02.");
            FailIf(rock.ShadowDrawn, "Even frame and part slot $d0 must suppress the falling shadow.");
            rock.SetPartSlot(1);
            FailIf(!rock.ShadowDrawn, "Even frame and part slot $d1 must draw the falling shadow.");
            _currentRoom.SetPositionTileAndCollision(target, 0x3a, 0, 0);
            FailIf(_currentRoom.GetTerrainInfo(target).Hazard != HazardType.None, "Volcano impact fixture requires dry terrain.");
            int fallFrames = (int)target.Y / 2;
            for (int i = 0; i < fallFrames; i++) Tick();
            FailIf(rock.State != 4 || rock.Z != 0 || sounds.Count != 0, "$11:$01 falling phase did not land at two pixels/update.");
            Tick();
            FailIf(rock.State != 5 || sounds.Count != 1 || sounds[0] != data.Constant("impact"),
                "$11:$01 impact omitted animation $03 / SND_STRONG_POUND.");
            var contactSave = OracleSaveData.CreateStandardGame();
            var contactInventory = new InventoryState(_treasures, contactSave);
            var contactPlayer = new Player { Name = "VolcanoContactPlayer" };
            AddChild(contactPlayer);
            try
            {
                contactPlayer.Initialize(new ValidationRingPlayerWorld(), contactInventory, target, new OracleRandom());
                int health = contactPlayer.HealthQuarters;
                rock.HandleLinkContact(contactPlayer);
                FailIf(contactPlayer.HealthQuarters != health - 2,
                    "$11:$01 doubled raw damage $fc must remove two quarter-hearts on contact.");
            }
            finally { contactPlayer.Free(); }
            for (int i = 0; i < 20; i++) Tick();
            FailIf(rock.Finished, "$11:$01 checked terminal $ff after instead of before animating.");
            Tick();
            FailIf(!rock.Finished || random.Calls != 2 || spawns.Count != 0,
                "$11:$01 impact failed to delete on its terminal parameter or consumed extra RNG.");
        }
        finally { rock.Free(); }

        // An airborne rock must draw at room Y + Z, and only presentation uses
        // camera offsets. A nonzero camera also participates in source landing arithmetic.
        random = new OracleRandom(); expectedRandom = new OracleRandom();
        expectedRandom.Next(); int value = expectedRandom.Next().Value;
        var lavaRock = new VolcanoRock(new(80, 24), data, _currentRoom, random, sounds.Add,
            point => point + new Vector2(-16, 0));
        try
        {
            void Tick() => lavaRock.UpdateFrame(new(_player, 0, false), spawns);
            for (int i = 0; i < 80 && lavaRock.State != 2; i++) Tick();
            Vector2 target = new(((value & 7) + 1) * 16 + 8 + 16, (value & 0x70) + 8 + 16);
            FailIf(lavaRock.Position != target, "$11 random landing coordinates lost hCameraX/Y.");
            _currentRoom.SetPositionTileAndCollision(target, 0xe4, null, 0);
            int soundCount = sounds.Count;
            for (int i = 0; i < 120 && !lavaRock.Finished; i++) Tick();
            FailIf(!lavaRock.Finished || sounds.Count != soundCount ||
                spawns is not [EnemySplashSpawn { Hazard: HazardType.Lava }],
                "$11:$01 must become INTERAC_LAVASPLASH on lava without a land impact.");
        }
        finally { lavaRock.Free(); }
        GD.Print("Validated volcanic rock fixed-point launch, correlated landing RNG, hidden delay, falling, impact and lava replacement.");
    }

    private void ValidateVolcanoShakeRng()
    {
        LoadValidationRoom(1, 3);
        var data = new VolcanoDatabase();
        var random = new OracleRandom();
        var spawns = new List<RoomEntitySpawn>();
        int y = 0, x = 0, magnitude = 0;
        var sounds = new List<int>();
        var controller = new VolcanoRoomEntity(new(1,3,1,0xb2,24,80), data, _currentRoom,
            _saveData, random, sounds.Add, (sy,sx,m) => { y=sy; x=sx; magnitude=m; },
            () => x != 0 || y != 0, m => magnitude=m, () => true);
        try
        {
            controller.UpdateFrame(new(_player, 0, false), spawns);
            FailIf(random.Calls != 0 || magnitude != 1, "$b2 state 0 must set magnitude without consuming RNG.");
            int calls = 0, rockCount = 0;
            for (int tick = 0; tick < 466; tick++)
            {
                bool nextStep = y == 0 && x == 0;
                if (nextStep) calls++;
                int before = spawns.Count;
                controller.UpdateFrame(new(_player, tick & 255, false), spawns);
                if (spawns.Count != before) { calls += 2; rockCount++; }
                FailIf(random.Calls != calls, $"$b2 script update {tick}: random counter/offset call order changed.");
                if (y != 0) y--;
                if (x != 0) x--;
            }
            FailIf(controller.ScriptIndex != 1 || controller.Counter != 254 || rockCount == 0 ||
                sounds.Count != 30 || spawns.OfType<VolcanoRockSpawn>().Any(s => s.Position.X < 72 || s.Position.X > 87),
                "$b2 seven-step loop, 16-frame rumble, or signed $f8..$07 launch offset changed.");
        }
        finally { controller.Node.Free(); }

        y = x = 0;
        int allocationAttempts = 0;
        int sourceCalls = 0;
        random = new OracleRandom();
        spawns.Clear();
        var fullParts = new VolcanoRoomEntity(new(1,3,1,0xb2,24,80), data, _currentRoom,
            _saveData, random, sounds.Add, (sy,sx,m) => { y=sy; x=sx; },
            () => x != 0 || y != 0, _ => { }, () => { allocationAttempts++; return false; });
        try
        {
            fullParts.UpdateFrame(new(_player, 0, false), spawns);
            for (int tick = 0; tick < 300; tick++)
            {
                if (y == 0 && x == 0) sourceCalls++;
                fullParts.UpdateFrame(new(_player, tick & 255, false), spawns);
                if (y != 0) y--;
                if (x != 0) x--;
            }
            FailIf(allocationAttempts == 0 || spawns.Count != 0 || random.Calls != sourceCalls + allocationAttempts * 2,
                "$b2 full part slots must reject allocation after consuming counter and X-offset RNG.");
        }
        finally { fullParts.Node.Free(); }

        var ambient = new VolcanoRoomEntity(new(1,2,0,5,0,0), data, _currentRoom,
            _saveData, random, sounds.Add, (sy,sx,m) => { y=sy; x=sx; magnitude=m; },
            () => x != 0 || y != 0, m => magnitude=m, () => true);
        try
        {
            ambient.UpdateFrame(new(_player, 1, false), spawns);
            FailIf(ambient.State != 2 || ambient.Counter is < 32 or > 159,
                "$dc:$05 must select active tremor on odd initialization and a $20..$9f duration.");
            int counter = ambient.Counter;
            ambient.UpdateFrame(new(_player, 3, false), spawns);
            FailIf(ambient.Counter != counter || x != 8 || y != 8, "$dc:$05 decremented on an odd frame.");
            var nextRandom = new OracleRandom();
            nextRandom.RestoreState(random.CaptureState());
            int quietCounter = (nextRandom.Next().Value & 0x7f) + 0x20;
            int callsBefore = random.Calls;
            for (int i = 0; i < counter; i++) ambient.UpdateFrame(new(_player, 4, false), spawns);
            FailIf(ambient.State != 1 || ambient.Counter != quietCounter || random.Calls != callsBefore + 1,
                "$dc:$05 state 2 must fall through to @setRandomShakeDuration on the zero update.");
            ambient.UpdateFrame(new(_player, 6, false), spawns);
            FailIf(ambient.Counter != quietCounter - 1 || x != 0 || y != 0,
                "$dc:$05 quiet interval did not begin with its newly selected timer.");
        }
        finally { ambient.Node.Free(); }

        _entities.LoadCutsceneRoom(1, _currentRoom, includeTimePortals: false);
        Vector2 offset = Vector2.Zero;
        void Observe(Vector2 point) => offset = point;
        _entities.ScreenShakeChanged += Observe;
        try
        {
            foreach (var axes in new[] { (Y: 1, X: 0), (Y: 0, X: 1), (Y: 1, X: 1) })
            {
                int before = _entities.RandomCalls;
                _entities.SetScreenShake(axes.Y, axes.X, 1);
                _entities.Update(1.0 / 60, _player);
                FailIf(_entities.RandomCalls != before + axes.Y + axes.X ||
                    Math.Abs(offset.Y) != axes.Y || Math.Abs(offset.X) != axes.X,
                    "bank1.updateScreenShake lost per-axis RNG or erased the final visible shake update.");
                _entities.Update(1.0 / 60, _player);
                FailIf(offset != Vector2.Zero || _entities.RandomCalls != before + axes.Y + axes.X,
                    "Expired screen shake failed to restore the camera without consuming RNG.");
            }
        }
        finally { _entities.ScreenShakeChanged -= Observe; }

        var firstPart = _entities.Spawn<VolcanoRock>(new VolcanoRockSpawn(new(80, 24)));
        var secondPart = _entities.Spawn<VolcanoRock>(new VolcanoRockSpawn(new(80, 24)));
        FailIf(firstPart.PartSlot != 0 || secondPart.PartSlot != 1,
            "$11 volcanic parts did not allocate source slots $d0 then $d1.");
        for (int i = 0; i < 200 && !firstPart.Finished; i++)
            firstPart.UpdateFrame(new(_player, i & 255, false), spawns);
        FailIf(!firstPart.Finished, "Volcanic part-slot reuse fixture did not finish its first rock.");
        var replacement = _entities.Spawn<VolcanoRock>(new VolcanoRockSpawn(new(80, 24)));
        FailIf(replacement.PartSlot != 0 || secondPart.PartSlot != 1,
            "getFreePartSlot must reuse $d0 without changing the surviving $d1 shadow parity.");
        GD.Print("Validated volcano script RNG calls, looping, signed offsets, rumble cadence and adjacent-room tremor counters.");
    }
}
