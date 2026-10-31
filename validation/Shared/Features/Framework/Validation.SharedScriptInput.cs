using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedScriptInput(GameRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var player = root._scene.Player;
        var first = new NativeScriptInputControl(memory);
        var second = new NativeScriptInputControl(memory);
        var native = new BraceletRom(rooms.CurrentRoom, player.PrecisePosition, 2, 0xff, game: game);
        native.LoadSave(rooms.SaveData);
        byte precedingIndex = memory.ReadWramByte(0xc2ef);
        byte precedingState = memory.ReadField("wCutsceneState");
        byte precedingRequest = memory.ReadField("wCutsceneTrigger");
        // Keep the fixture's stack away from wThreadStateBuffer; BraceletRom's
        // script wrapper deliberately uses $c2f0 and would overwrite $c2ef.
        var requests = new LinkCollisionRom(game: game);
        try
        {
            foreach (byte request in new byte[] { 0, 0x13, 0x93, 0x81 })
            {
                memory.SetWramByte(0xc2ef, 0x55); memory.SetField("wCutsceneState", 0xab); memory.SetField("wCutsceneTrigger", request);
                requests[0xc2ef] = 0x55; requests[0xcc03] = 0xab; requests[0xcc04] = request;
                int selected = -1;
                bool consumed = NativeCutsceneRequest.TrySelect(memory, index => selected = index);
                if (request != 0) requests.Call(game == OracleGame.Ages ? 0x5e3d : 0x5cb4, bank: 1);
                FailIf(consumed != (request != 0) || selected != (request == 0 ? -1 : request & 0x7f) ||
                    memory.ReadWramByte(0xc2ef) != requests[0xc2ef] || memory.ReadField("wCutsceneState") != requests[0xcc03] ||
                    memory.ReadField("wCutsceneTrigger") != requests[0xcc04],
                    $"{game}: shared request ${request:x2} must preserve the original index masking, state reset and request clear.");
            }
        }
        finally
        {
            memory.SetWramByte(0xc2ef, precedingIndex); memory.SetField("wCutsceneState", precedingState); memory.SetField("wCutsceneTrigger", precedingRequest);
        }
        first.SetEnabled(player, false); native.SetScriptInputEnabled(false);
        FailIf(memory.ReadField("wDisabledObjects") != native[0xcc8a] ||
            memory.ReadField("wMenuDisabled") != native[0xcc02] ||
            player.InvincibilityFrames != unchecked((sbyte)native[0xd02b]) ||
            player.KnockbackFrames != native[0xd02d] || player.CutsceneControlled,
            $"{game}: native script input must write $81/$80 and invincibility without replacing Link's handler.");
        Vector2 position = player.PrecisePosition;
        int clock = memory.FrameCounter;
        new ApplicationValidationFixture(root).Step(3, Vector2.Right, batched: batched);
        FailIf(player.PrecisePosition != position || memory.FrameCounter != ((clock + 3) & 255),
            $"{game}: native script masks must freeze Link while the real shared main thread continues.");

        second.SetEnabled(player, false);
        first.Cancel();
        Require(0x81, 0x80, "a later identical disable write must survive cancellation of the first script");
        second.Cancel();
        Require(0, 0, "the current script must release both native control bytes");

        first.SetEnabled(player, false);
        memory.SetField("wDisabledObjects", 0x91);
        first.Cancel();
        Require(0x91, 0, "a later object-mask writer must survive while the old menu byte is released");
        first.SetEnabled(player, false);
        memory.SetWramByte(memory.FieldAddress("wMenuDisabled"), 0x80);
        first.Cancel();
        Require(0, 0x80, "a later identical raw menu write must survive while the old object byte is released");

        first.SetDisabledObjects(0x91);
        second.SetEnabled(player, false);
        first.Cancel();
        Require(0x81, 0x80, "a replaced $91 mask must not erase a later script's input control");
        second.Cancel();
        NativeScriptInputControl.SetEnabled(memory, player, false);
        var snapshot = memory.CaptureState();
        first.SetEnabled(player, false);
        memory.RestoreState(snapshot);
        first.Cancel();
        Require(0x81, 0x80, "restored bytes must invalidate the previous live script's cancellation authority");
        first.SetEnabled(player, false);
        first.Relinquish(); first.Cancel();
        Require(0x81, 0x80, "a native actor's explicit control handoff must survive its retirement");
        NativeScriptInputControl.SetEnabled(memory, player, true);
        native.SetScriptInputEnabled(true);
        Require(native[0xcc8a], native[0xcc02], "enableinput must retain its native zero writes");

        first.SetScreenTransitionsDisabled(1);
        second.SetScreenTransitionsDisabled(1);
        first.Cancel();
        FailIf(memory.ReadField("wDisableScreenTransitions") != 1,
            $"{game}: cancellation must preserve a later identical native transition lock.");
        second.Cancel();
        FailIf(memory.ReadField("wDisableScreenTransitions") != 0,
            $"{game}: the current owner must release its native transition lock.");
        first.SetScreenTransitionsDisabled(1);
        var transitionSnapshot = memory.CaptureState();
        memory.RestoreState(transitionSnapshot);
        first.Cancel();
        FailIf(memory.ReadField("wDisableScreenTransitions") != 1,
            $"{game}: restoring native transition bytes must invalidate old cancellation ownership.");
        first.SetScreenTransitionsDisabled(0);

        first.SetDisabledObjects(0x11); native[0xcc8a] = 0x11;
        ulong objectWrite = memory.ObjectsDisabledWriteVersion;
        first.SetMenuEnabled(player, false); native.SetScriptMenuEnabled(false);
        Require(native[0xcc8a], native[0xcc02], "disablemenu must preserve $11 while writing its native $80 menu byte");
        FailIf(memory.ObjectsDisabledWriteVersion != objectWrite ||
            player.InvincibilityFrames != unchecked((sbyte)native[0xd02b]) || player.KnockbackFrames != native[0xd02d],
            $"{game}: standalone disablemenu must retain its native physical effects without writing wDisabledObjects.");
        first.SetMenuEnabled(player, true); native.SetScriptMenuEnabled(true);
        Require(native[0xcc8a], native[0xcc02], "enablemenu must preserve the object mask");
        FailIf(memory.ObjectsDisabledWriteVersion != objectWrite, $"{game}: enablemenu must not invalidate an object-mask owner's cancellation.");
        first.Cancel();
        Require(0, 0, "the independent object owner must retain cleanup authority after menu-only commands");
        first.SetMenuEnabled(player, false);
        position = player.PrecisePosition;
        new ApplicationValidationFixture(root).Step(3, Vector2.Right, batched: batched);
        FailIf(player.PrecisePosition == position, $"{game}: disablemenu alone must retain normal Link movement in the real shared loop.");
        first.Cancel();

        native.SetScriptInputEnabled(true);
        foreach (byte mask in new byte[] { 0, 0x80 })
        {
            first.SetDisabledObjects(mask); native[0xcc8a] = mask;
            player.RequestNativeState08Control(first); native.RequestState08();
            for (int update = 1; update <= 3; update++)
            {
                new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
                native.UpdateLinkItems(memory.FrameCounter, 0);
                bool normal = update == 3 && mask == 0;
                FailIf((native[0xd004] == 1) != normal || player.NativeNormalStateForInteraction != normal,
                    $"{game}: native state$08 must return solely on the object mask at substate1; mask=${mask:x2}, update={update}.");
            }
            first.SetDisabledObjects(0); native[0xcc8a] = 0;
            new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
            native.UpdateLinkItems(memory.FrameCounter, 0);
            FailIf(native[0xd004] != 1 || !player.NativeNormalStateForInteraction || player.CutsceneControlled,
                $"{game}: clearing the native mask must release state$08 without a private owner callback.");
        }

        first.SetDisabledObjects(0x80); native[0xcc8a] = 0x80;
        player.RequestNativeState08Control(first); native.RequestState08();
        for (int update = 0; update < 3; update++)
        {
            new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
            native.UpdateLinkItems(memory.FrameCounter, 0);
        }
        memory.SetField("wLinkForceState", 4);
        memory.SetField("wcc50", 0x80);
        native.RequestGetItemState(0x80);
        for (int update = 1; update <= 3; update++)
        {
            new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
            native.UpdateLinkItems(memory.FrameCounter, 0);
            bool normal = update == 3;
            FailIf((native[0xd004] == 1) != normal || player.NativeNormalStateForInteraction != normal || player.CutsceneControlled,
                $"{game}: the state$08->$04->$01 handoff must retire old control at the native force-state boundary; update={update}.");
            FailIf(native[0xd004] == 4 && player.CanMountCompanion,
                $"{game}: commonCode.s:companionTryToMount must reject Link's native item-get state $04.");
            if (update == 1)
            {
                memory.SetField("wcc50", 0x01); native[0xcc50] = 0x01;
            }
            if (update == 2)
            {
                FailIf(!player.IsHoldingItemTwoHands || native[0xd030] != 0x0f,
                    $"{game}: state04 initialization must read the live parameter rather than a request-time copy.");
                memory.SetField("wcc50", 0x80); native[0xcc50] = 0x80;
            }
        }
        first.SetDisabledObjects(0); native[0xcc8a] = 0;

        player.Inventory.GiveTreasure(TreasureId.Sword, 1);
        native.LoadSave(rooms.SaveData);
        first.SetDisabledObjects(0x81); native[0xcc8a] = 0x81;
        player.RequestNativeState08Control(first, 0xff); native.RequestState08(); native[0xcc50] = 0xff;
        for (int update = 0; update < 2; update++)
        {
            new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
            native.UpdateLinkItems(memory.FrameCounter, 0);
            FailIf(memory.ReadField("wcc50") != native[0xcc50],
                $"{game}: state08 must consume its live $ff parameter on initialization, update={update + 1}.");
        }
        memory.SetField("wcc63", 0xff); native[0xcc63] = 0xff;
        int spinUpdates = 0;
        do
        {
            new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
            native.UpdateLinkItems(memory.FrameCounter, 0);
            spinUpdates++;
            CompareParentAnimationPublicationRom(player, address => native[address], $"{game}: forced Sword spin update{spinUpdates}");
            FailIf(memory.ReadField("wcc63") != native[0xcc63] || player.IsAttacking != (native[0xd204] == 4),
                $"{game}: state08 forced sword parent differs at update {spinUpdates}, cc63=${memory.ReadField("wcc63"):x2}/${native[0xcc63]:x2}, attacking={player.IsAttacking}, parent=${native[0xd204]:x2}.");
        } while (native[0xcc63] != 0 && spinUpdates < 70);
        FailIf(spinUpdates >= 70, $"{game}: forced sword spin did not finish under the $81 object mask.");
        memory.SetField("wSwordDisabledCounter", 2); native[0xcc59] = 2;
        memory.SetField("wcc63", 0xff); native[0xcc63] = 0xff;
        new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
        native.UpdateLinkItems(memory.FrameCounter, 0);
        FailIf(memory.ReadField("wSwordDisabledCounter") != 1 || native[0xcc59] != 1 ||
            memory.ReadField("wcc63") != 0 || native[0xcc63] != 0 || player.IsAttacking,
            $"{game}: forced sword initialization must consume the shared disable counter and reject the parent on that same update.");
        CompareParentAnimationPublicationRom(player, address => native[address], $"{game}: rejected forced Sword initialization");
        new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
        native.UpdateLinkItems(memory.FrameCounter, 0);
        FailIf(memory.ReadField("wSwordDisabledCounter") != 1 || native[0xcc59] != 1,
            $"{game}: state08 must stop advancing the sword-disable counter after wcc63 is cleared.");
        CompareParentAnimationPublicationRom(player, address => native[address], $"{game}: retired forced Sword parent");
        memory.SetField("wSwordDisabledCounter", 0); native[0xcc59] = 0;
        memory.SetField("wcc50", 0x0e); native[0xcc50] = 0x0e;
        new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
        native.UpdateLinkItems(memory.FrameCounter, 0);
        FailIf(memory.ReadField("wcc50") != 0 || native[0xcc50] != 0 || !player.IsHoldingItemOneHand || native[0xd030] != 0x0e,
            $"{game}: state08 must consume the raised-sword animation request on Link's next dispatch.");
        first.SetDisabledObjects(0); native[0xcc8a] = 0;
        new ApplicationValidationFixture(root).Step(1, Vector2.Zero, batched: batched);
        native.UpdateLinkItems(memory.FrameCounter, 0);
        FailIf(!player.NativeNormalStateForInteraction || native[0xd004] != 1 || player.IsHoldingItemOneHand,
            $"{game}: clearing the mask must retire the forced sword pose on the next Link dispatch.");

        FailIf(memory.FieldAddress("wDisableLinkCollisionsAndMenu")!=0xcbca,
            $"{game}: the collision/menu lock must bind native$cbca independently of wMenuDisabled.");
        byte savedMenu=memory.ReadField("wMenuDisabled");
        first.SetLinkCollisionsAndMenuDisabled(0x81);
        second.SetLinkCollisionsAndMenuDisabled(0x81);first.Cancel();
        FailIf(memory.ReadWramByte(0xcbca)!=0x81 || memory.ReadField("wMenuDisabled")!=savedMenu,
            $"{game}: cancelling an older collision/menu lock must preserve its successor and the distinct menu byte.");
        second.Cancel();
        FailIf(memory.ReadWramByte(0xcbca)!=0,$"{game}: the live collision/menu lock owner must release its byte.");
        first.SetLinkCollisionsAndMenuDisabled(1);memory.SetWramByte(0xcbca,1);first.Cancel();
        FailIf(memory.ReadWramByte(0xcbca)!=1,$"{game}: a later identical raw collision/menu write must supersede cancellation.");
        memory.SetWramByte(0xcbca,0);
        first.SetLinkCollisionsAndMenuDisabled(1);var lockSnapshot=memory.CaptureState();memory.RestoreState(lockSnapshot);first.Cancel();
        FailIf(memory.ReadWramByte(0xcbca)!=1,$"{game}: restoring native bytes must retire the old collision/menu cancellation lease.");
        memory.SetWramByte(0xcbca,0);

        if (game == OracleGame.Ages)
        {
            var host = root._roomEvents.Get<PlenEvent>();
            host.SetInputEnabled(false);
            Require(0x81, 0x80, "room-script commands must use the same native input operation");
            FailIf(player.CutsceneControlled, "Native room-script disableinput must retain Link's normal handler.");
            memory.SetField("wMenuDisabled", 0x80);
            host.Cancel();
            Require(0, 0x80, "room-script cancellation must preserve a later identical menu write");
            NativeScriptInputControl.SetEnabled(memory, player, true);
        }

        void Require(int objects, int menu, string context) =>
            FailIf(memory.ReadField("wDisabledObjects") != objects || memory.ReadField("wMenuDisabled") != menu,
                $"{game}, batch={batched}: {context}.");
    }
}
