using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedScrollLifetimes(FrontendValidationRoot root, bool batched)
    {
        int sourceGroup = root._gameplaySession!.Rooms.ActiveGroup;
        int sourceRoom = root._gameplaySession.Rooms.CurrentRoom.Id;
        foreach (bool warm in new[] { false,true })
        foreach (bool cancel in new[] { false,true })
        {
            var engine = root._gameplaySession!;
            var rooms = engine.Rooms;
            var entities = root._seasonsOpening?.Entities ?? root._entities;
            var player = root._scene.Player;
            var inventory = player.Inventory;
            var transitions = engine.Transitions;
            var app = new ApplicationValidationFixture(root);
            OracleGame game = rooms.SaveData.Game;
            if (game == OracleGame.Ages) root.LoadDebugRoom(sourceGroup,sourceRoom);
            else root._seasonsOpening!.DebugTravel(sourceGroup,sourceRoom);
            entities.Clear(); player.WarpTo(new(72,80));
            inventory.EquipA(0); inventory.EquipB(0);
            var restore = root.CaptureDebugSavestate();
            var satchel = ((PlayerWorld)player.World).Satchel;
            satchel.Pegasus.Clear();
            inventory.GiveTreasure(TreasureId.SeedSatchel,1);
            inventory.GiveTreasure(TreasureId.PegasusSeeds,0x20);
            inventory.SelectSatchelSeeds(2); inventory.EquipB(TreasureId.SeedSatchel);
            var native = new BraceletRom(rooms.CurrentRoom,player.Position,0,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 0; native[0xcd00] = 1;
            app.Step(1,Vector2.Zero,held:["item"],pressed:["item"],batched:batched);
            native.UpdateLinkItems(entities.FrameCounter,2);
            if (warm)
                app.Step(32,Vector2.Zero,batched:batched,
                    afterUpdate:() => native.UpdateLinkItems(entities.FrameCounter,0));
            else
            {
                // Declared pending reserved child, at the same footprint written
                // by the Satchel parent; the input path itself was exercised above.
                entities.RetireEntity(entities.Entities<PegasusDustRoomEntity>().Single());
                entities.RequestPegasusDust(satchel.Pegasus,signal:false);
                for (int offset = 0; offset < 0x40; offset++) native[0xdf00 + offset] = 0;
                native[0xdf00] = 3; native[0xdf01] = 0x1a;
            }
            var dust = entities.Entities<PegasusDustRoomEntity>().Single();
            var target = new BurningEnemyValidationTarget();
            var flame = entities.Spawn<BurningEnemyPart>(new BurningEnemySpawn(target));
            // Controlled related-object bytes isolate PART $12 from species AI.
            native[0xd180] = 1; native[0xd181] = (byte)target.BurnTargetId; native[0xd1a9] = (byte)target.BurnHealth;
            native.Word(0xd18a,(int)target.BurnPosition.Y * 256); native.Word(0xd18c,(int)target.BurnPosition.X * 256);
            native.Word(0xd18e,target.BurnZFixed);
            native[0xd0c0] = 1; native[0xd0c1] = 0x12; native.Word(0xd0d6,0xd180);
            rooms.SaveData.SetGlobalFlag(game == OracleGame.Ages ? 0x16 : 0x2f,false);
            if (game == OracleGame.Ages) rooms.RuntimeState.SetField("wSentBackByStrangeForce",0);
            int pack = rooms.RuntimeState.ReadField("wLoadingRoomPack"), season = rooms.RuntimeState.ReadField("wRoomStateModifier");
            var bannerNative = new RoomInfoRom(rooms.SaveData,rooms.CurrentRoom,pack,season);
            FailIf(!bannerNative.TryDisplay() || !EraOrSeasonInfoDatabase.For(rooms.AssetRoot).TryDisplay(rooms,entities),
                $"{game}: scroll fixture requires the native optional banner allocation.");
            var banner = entities.Entities<EraOrSeasonInfoDisplay>().Single();
            var mark=(ExclamationMarkRoomEntity)entities.TryCreateExclamation(new(72,64),30)!;
            var markNode=(NpcCharacter)mark.Node;
            int markSlot=0xd040+entities.NativeInteractionSlot(mark)*256;
            native[markSlot]=1;native[markSlot+1]=0x9f;native[markSlot+6]=30;
            native[markSlot+0xb]=64;native[markSlot+0xd]=72;
            if (warm)
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:() =>
                { native.UpdateLinkItems(entities.FrameCounter,0,interactions:true); native.AdvanceParts(); bannerNative.Update(); });
            bool selected = false;
            Vector2I direction = default;
            int destination = 0;
            foreach (var candidate in new[] { Vector2I.Right,Vector2I.Left,Vector2I.Down,Vector2I.Up })
                if (transitions.TryGetScreenTransitionDestination(candidate,out int room) &&
                    !rooms.RequiresRoomPackFade(rooms.ActiveGroup,rooms.CurrentRoom.Id,room))
                { direction = candidate; destination = room; selected = true; break; }
            FailIf(!selected,$"{game}: actual scroll fixture requires a supported neighbor in the same room pack.");
            transitions.BeginValidationScroll(player,direction,destination);
            FailIf(!transitions.ScrollActive || !entities.ScreenTransitionActive || rooms.CurrentRoom.Id != destination,
                $"{game}: fixture must enter the real shared scroll lifecycle.");
            native.BeginScroll(); bannerNative.BeginScroll();
            int incomingUpdates = 0;
            entities.AddEntity(new ItemPhaseValidationEntity(() => incomingUpdates++));
            int updates = 0, counter = satchel.Pegasus.RawCounter;
            void Step(int count)
            {
                // Link's scroll motion has separate native coverage. Supply
                // the pre-object coordinates to the native dust-copy phase.
                Vector2 link = player.PrecisePosition;
                app.Step(count,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    native.Word(0xd00c,(int)(link.X * 256)); native.Word(0xd00a,(int)(link.Y * 256));
                    native.UpdateLinkItems(entities.FrameCounter,0,interactions:true); native.AdvanceParts(); bannerNative.Update();
                    updates++;
                    if (!transitions.ScrollActive) { native.FinishScroll(); bannerNative.FinishScroll(); }
                    bool bannerAlive = entities.OutgoingEntities<EraOrSeasonInfoDisplay>().Contains(banner) ||
                        entities.Entities<EraOrSeasonInfoDisplay>().Contains(banner);
                    bool flameAlive = entities.OutgoingEntities<BurningEnemyPart>().Contains(flame) ||
                        entities.Entities<BurningEnemyPart>().Contains(flame);
                    string context = $"{game}: actual scroll warm={warm}, cancel={cancel}, update={updates}";
                    bool markAlive=entities.OutgoingEntities<NpcCharacter>().Contains(markNode) || entities.Entities<NpcCharacter>().Contains(markNode);
                    FailIf(markAlive!=(native[markSlot]!=0) || markAlive &&
                        (mark.Counter!=native[markSlot+6] || markNode.Visible!=((native[markSlot+0x1a]&128)!=0)),
                        context+": common exclamation initialization/always-update counter/visibility/retirement differs.");
                    FailIf(incomingUpdates != 0 || satchel.Pegasus.RawCounter != counter || native.Word(0xcc6c) != counter,
                        context + ": incoming objects or Link's Pegasus counter advanced during the scroll.");
                    FailIf(bannerAlive != (bannerNative[0] != 0) || bannerAlive &&
                        ((int)banner.Stage != bannerNative[4] || banner.Counter != bannerNative[6] ||
                        unchecked((byte)(int)banner.Position.X) != bannerNative[13]),context + ": banner dispatch/retirement differs.");
                    FailIf(flameAlive != (native[0xd0c0] != 0) || flameAlive &&
                        (flame.Counter != native[0xd0c6] || target.BurnHealth != native[0xd1a9] ||
                        target.BurnZFixed != unchecked((short)native.Word(0xd18e))),context + ": burning initialization/freeze/retirement differs.");
                    bool dustAlive = entities.OutgoingEntities<PegasusDustRoomEntity>().Contains(dust) ||
                        entities.Entities<PegasusDustRoomEntity>().Contains(dust);
                    FailIf(!dustAlive || native[0xdf00] != 3 || dust.Substate != native[0xdf05] ||
                        dust.Position != new Vector2(native[0xdf0d],native[0xdf0b]) || dust.Visible != ((native[0xdf1a] & 0x80) != 0) ||
                        dust.TileBase != native[0xdf1d] || dust.OamFlags != native[0xdf1c] ||
                        !dust.Clouds.SequenceEqual(Enumerable.Range(0,8).Select(offset => native[0xdf30 + offset]).ToArray()),
                        context + ": reserved dust updates/persistence differ.");
                    link = player.PrecisePosition;
                });
            }
            Step(5);
            FailIf(!transitions.ScrollActive || flame.Counter != (warm ? 56 : 58),
                $"{game}: PART $12 must initialize once, then freeze in the actual scroll.");
            if (!cancel)
            {
                Step(transitions.ScrollTotalFrames - updates);
                FailIf(transitions.IsTransitioning || entities.ScreenTransitionActive || target.ReleaseCalls != 0 ||
                    !entities.Entities<PegasusDustRoomEntity>().Contains(dust),$"{game}: completion must clear outgoing effects while retaining reserved dust.");
                app.Step(2,Vector2.Zero,batched:batched);
                FailIf(incomingUpdates != 2,$"{game}: destination objects must resume after the finishing scroll update.");
            }
            else
            {
                root.RestoreDebugSavestate(restore);
                FailIf(engine.Active || transitions.IsTransitioning || transitions.ScrollActive || !entities.IsDisposed ||
                    entities.ScreenTransitionActive || entities.OutgoingEntities<EraOrSeasonInfoDisplay>().Count != 0 ||
                    entities.OutgoingEntities<BurningEnemyPart>().Count != 0 || entities.OutgoingEntities<PegasusDustRoomEntity>().Count != 0,
                    $"{game}: replacing a session during scrolling must retire its transition and all outgoing allocations.");
                var callback = (MulticastDelegate?)typeof(RoomSession).GetField("RoomChanged",BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rooms);
                FailIf(callback?.GetInvocationList().Any(handler => ReferenceEquals(handler.Target,transitions)) == true,
                    $"{game}: retired transition owner remains subscribed to room changes.");
                int clock = rooms.RuntimeState.FrameCounter, rng = entities.Random.Calls;
                engine.Advance(); transitions.UpdateScroll(ApplicationFixedUpdateScheduler.UpdateDelta);
                transitions.UpdateWarp(ApplicationFixedUpdateScheduler.UpdateDelta); transitions.UpdateCamera();
                bool rejected = false;
                try { transitions.BeginScroll(player,direction,destination); }
                catch (ObjectDisposedException) { rejected = true; }
                FailIf(!rejected,$"{game}: retired transition owner accepted a new scroll request.");
                rooms.RuntimeState.SetField("wScrollMode",1);
                rooms.RuntimeState.SetField("wScreenTransitionDirection",0x82);
                transitions.CheckRoomExit(player);
                FailIf(rooms.RuntimeState.ReadField("wScrollMode") != 1 ||
                    rooms.RuntimeState.ReadField("wScreenTransitionDirection") != 0x82,
                    $"{game}: retired boundary polling must not consume or publish a request.");
                rejected = false;
                try { transitions.BeginLedgeScroll(player); }
                catch (ObjectDisposedException) { rejected = true; }
                FailIf(!rejected || rooms.RuntimeState.ReadField("wScreenTransitionDirection") != 0x82,
                    $"{game}: retired ledge commands must reject before mutating the request owner.");
                rejected = false;
                try { transitions.ApplyWarp(player,new Warp(sourceGroup,sourceRoom,0x44,0,0,sourceGroup,sourceRoom,0x44,0,0)); }
                catch (ObjectDisposedException) { rejected = true; }
                FailIf(!rejected,$"{game}: retired transition owner accepted a new warp request.");
                rejected = false;
                try { transitions.RequestNativeFadeOutWarp(new Warp(sourceGroup,sourceRoom,0x44,0,0,sourceGroup,sourceRoom,0x44,0,0)); }
                catch (ObjectDisposedException) { rejected = true; }
                FailIf(!rejected,$"{game}: retired transition owner accepted a queued native warp.");
                if (transitions is AgesRoomTransitionController ages)
                {
                    rejected = false;
                    try { ages.ApplyTimePortalWarp(player,Vector2.Zero); }
                    catch (ObjectDisposedException) { rejected = true; }
                    FailIf(!rejected,"Retired Ages mode $06 must not reactivate its independent native sequence.");
                }
                FailIf(rooms.RuntimeState.FrameCounter != clock || entities.Random.Calls != rng || target.ReleaseCalls != 0,
                    $"{game}: retired scroll work must not advance or restore a detached burn target.");
                app.Step(2,Vector2.Zero,batched:batched);
                FailIf(root._gameplaySession is not { Active: true } next || ReferenceEquals(next,engine) ||
                    next.Rooms.CurrentRoom.Id != sourceRoom || next.Transitions.IsTransitioning,
                    $"{game}: replacement must leave a fresh usable session at its captured source room.");
            }
        }
        if (root._selectedGame == OracleGame.Ages) root.LoadDebugRoom(sourceGroup,sourceRoom);
        else root._seasonsOpening!.DebugTravel(sourceGroup,sourceRoom);
        (root._seasonsOpening?.Entities ?? root._entities).Clear();
        root._scene.Player.Inventory.EquipA(0); root._scene.Player.Inventory.EquipB(0);
        ((PlayerWorld)root._scene.Player.World).Satchel.Pegasus.Clear();
    }
}
