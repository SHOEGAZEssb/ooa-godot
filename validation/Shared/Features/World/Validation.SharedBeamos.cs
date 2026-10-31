using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBeamos(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        var initialRandom = entities.Random.CaptureState();
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        void Load(int group, int room)
        {
            root._scene.Dialogue.Close();
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
            entities.Clear(); entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0); player.Inventory.SetScriptedActiveRing(0xff);
        }
        try
        {
            var entry = new DebugObjectCatalog(rooms.AssetRoot).Enemies.Single(value => value.Id == 0x16 && value.SubId == 0);
            int previewCalls = entities.Random.Calls;
            var texture = new DebugObjectPreview(rooms.AssetRoot).GetTexture(entry, false);
            FailIf(!entry.Supported || texture is null || entities.Random.Calls != previewCalls,
                $"{game}: the native Beamos picker entry must offer a pack-bound preview without initializing an actor.");
            using (Image image = texture!.GetImage())
                FailIf(image.GetUsedRect().Size.X == 0, $"{game}: Beamos preview has no source pixels.");
            var data = BeamosDatabase.For(rooms.AssetRoot).Behavior;
            int[] angles = [0,0,1,1,1,1,1,2,2,2,3,3,3,3,3,4,4,4,5,5,5,5,5,6,6,6,7,7,7,7,7,0];
            int[] beamAngles = [0,0,1,2,2,2,3,4,4,4,5,6,6,6,7,0];
            FailIf(data.RotationFrames != 5 || data.CooldownFrames != 40 || data.SoundCounter != 11 ||
                data.ChargeFrames != 20 || data.GlowFrames != 20 || data.TargetAngleWindow != 2 || data.BeamBlinkMask != 1 ||
                data.BeamCollisionDelay != 2 || data.BeamSpeed != 0x50 || data.BeamVelocityScale != 4 ||
                !data.AngleAnimations.Select(value => value.Value).SequenceEqual(angles) ||
                !data.BeamAngleAnimations.Select(value => value.Value).SequenceEqual(beamAngles),
                $"{game}: common enemyCode16/partCode29 source operands changed.");
            foreach (bool pressure in new[] { false, true })
            {
                Load(0, game == OracleGame.Ages ? 0x98 : 0xc9);
                var room = rooms.CurrentRoom;
                byte floor = game == OracleGame.Ages ? (byte)0xa0 : (byte)0x04;
                for (int y=0; y<room.HeightInTiles; y++)
                for (int x=0; x<room.WidthInTiles; x++)
                    room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), floor, 0, 0);
                player.WarpTo(new(80, 40));
                var seed = entities.Random.CaptureState();
                FailIf(!entities.TrySpawnDebugEnemy(0x16, 0, new(80, 72), out string error), error);
                var eye = entities.Entities<BeamosCharacter>().Single();
                FailIf(!eye.InitializationPending || eye.Visible || eye.AssetRoot != rooms.AssetRoot || entities.Random.Calls != seed.Calls,
                    $"{game}: Beamos must allocate from its selected pack without executing native state zero.");
                var native = SharedNativeEnemyReference(rooms, player, 0x16);
                // This AI/part fixture supplies the already-loaded projectile
                // sheet; native VRAM loading time is outside its object loop.
                native.SetResidentObjectGraphics(2, game == OracleGame.Ages ? 0xa7 : 0x8f);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                native[0xd080] = 1; native[0xd081] = 0x16;
                native.Word(0xd08a, 72 * 256); native.Word(0xd08c, 80 * 256);
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(entities, "_partSlots");
                if (pressure)
                {
                    memory.SetField("wDisabledObjects", 8); native[0xcc8a] = 8;
                    for (int index=0; index<16; index++)
                    {
                        var actor = entities.Spawn<BeamosBeamPart>(new BeamosBeamSpawn(new(32, 96), 8, index & 1));
                        int slot = slots.Single(pair => ReferenceEquals(pair.Key.PresentationNode, actor)).Value;
                        int at = 0xd0c0 + slot * 256;
                        native[at] = 1; native[at + 1] = 0x29; native[at + 2] = (byte)(index & 1); native[at + 9] = 8;
                        native.Word(at + 10, 96 * 256); native.Word(at + 12, 32 * 256);
                    }
                }
                var sounds = root._sound.AttachPlayRequestAudit();
                int updates = 0, beamVisits = 0;
                void Compare()
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    string context = $"{game}: Beamos pressure={pressure}, batch={batched}, update{updates}";
                    var rng = entities.Random.CaptureState();
                    FailIf(eye.State != native[0xd084] || eye.Counter != native[0xd086] || eye.Cooldown != native[0xd087] ||
                        eye.Angle != native[0xd089] || eye.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) ||
                        eye.Visible != ((native[0xd09a] & 0x80) != 0) ||
                        rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls ||
                        sounds.RequestsFor(SoundId.SndBeam) != native.Sounds.Count(sound => sound == 0xa4),
                        context + ": native turret state, counters, angle, visibility, cue or RNG differs.");
                    FailIf(room.GetTerrainInfo(eye.Position).Collision != 0x0f || native[0xce45] != 0x0f,
                        context + ": state zero must solidify its source tile.");
                    for (int slot=0; slot<16; slot++)
                    {
                        int at = 0xd0c0 + slot * 256;
                        var entity = slots.FirstOrDefault(pair => pair.Value == slot && pair.Key is not IRoomEntityLifetime { Finished: true }).Key;
                        FailIf((entity is null) != (native[at] == 0), $"{context}: PART${0xd0 + slot:x2} occupancy differs.");
                        if (entity is null) continue;
                        var segment = (BeamosBeamPart)entity.PresentationNode!;
                        var position = SomariaPrivate<OracleObjectPosition>(segment, "_position");
                        var velocity = SomariaPrivate<OracleObjectVelocity>(segment, "_velocity");
                        beamVisits++;
                        FailIf(native[at + 1] != 0x29 || segment.AssetRoot != rooms.AssetRoot ||
                            segment.SubId != native[at + 2] || segment.State != native[at + 4] || segment.Counter != native[at + 6] ||
                            segment.Angle != native[at + 9] || position.YFixed != native.Word(at + 10) || position.XFixed != native.Word(at + 12) ||
                            (ushort)velocity.YFixed != native.Word(at + 16) || (ushort)velocity.XFixed != native.Word(at + 18) ||
                            segment.Visible != ((native[at + 0x1a] & 0x80) != 0),
                            $"{context}: PART${0xd0 + slot:x2} native initialization, components, motion or blink differs.");
                    }
                }
                void Step(int count) => app.Step(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
                root._scene.Dialogue.ShowMessage("Native Beamos state-zero hold.", player.Position.Y); native[0xcba0] = 1;
                Step(3);
                FailIf(eye.State != 8 || eye.Counter != 5, "Beamos state zero must initialize under text without advancing its ordinary state.");
                root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(23);
                if (pressure)
                {
                    FailIf(entities.Entities<BeamosBeamPart>().Count != 16 || sounds.RequestsFor(SoundId.SndBeam) != 1,
                        "Full PART capacity must suppress all ten Beamos births while preserving the source cue and cooldown.");
                    memory.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
                }
                Step(240);
                FailIf(beamVisits == 0 || sounds.RequestsFor(SoundId.SndBeam) < 2,
                    "The shared Beamos constructor must reach repeated native firing and independently updated PART beams.");
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects", 0);
            player.EndCutsceneControl(); Load(originalGroup, originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu(); entities.Random.RestoreState(initialRandom);
        }
    }
}
