
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBossInitializationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (var source in new[] { (Id:0x79, Room:0x2b),(Id:0x71,Room:0x34),(Id:0x72,Room:0x4d),
            (Id:0x78,Room:0x13),(Id:0x7a,Room:0x4a) })
        foreach (bool fullParts in source.Id == 0x71 ? new[] { false,true } : new[] { false })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,source.Room,0x80,false); LoadValidationRoom(4,source.Room);
            var owner = _entities.EntityAdapters<IRoomEntity>().Single(entity => source.Id switch
            {
                0x79 => entity.Node is HeadThwompBoss, 0x71 => entity.Node is SwoopBoss,
                0x72 => entity.Node is SubterrorBoss, 0x78 => entity.Node is PumpkinHeadBoss,
                _ => entity.Node is ShadowHagBoss
            });
            F1Retain(owner);
            FailIf(!F8Enemies.TryGetValue(owner,out int slot) || slot != 0,
                $"ENEMY${source.Id:x2}: actual placement must bind its native enemy slot before initialization.");
            _inventory.SetScriptedEquippedItems(0,0);
            // Declared clear floor and frozen state8 isolate the common state0
            // handoff; this is not a boss encounter or a room-approach proof.
            FillValidationRoom(0xa2,width:15,height:11);
            _player.WarpTo(new(16,160)); _player.Face(Vector2I.Down);
            _runtimeState.SetField("wScrollMode",8); _runtimeState.SetField("wScreenTransitionDirection",1);
            _runtimeState.SetField("wLinkForceState",0); _runtimeState.SetField("wPaletteThread_mode",0);
            _runtimeState.SetField("wDisabledObjects",0); _runtimeState.SetField("wcc93",0);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var native = F1Native(source.Id,0,0,owner.Node.Position);
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            native[0xd080]=0x11; native[0xd008]=2; native[0xd009]=0xff; native[0xd01a]=0xc1;
            native[AgesWramAddress.wWarpsDisabled]=1;
            foreach (string field in new[] { "wScrollMode","wScreenTransitionDirection","wLinkForceState",
                "wLinkStateParameter","wcc50","wcc52","wDisabledObjects","wcc93" })
                native[_runtimeState.FieldAddress(field)]=_runtimeState.ReadField(field);
            if (fullParts) F1Fill(0,native);
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            var prepare = typeof(RoomEntityManager).GetMethod("PrepareIncomingEntitiesForScreenTransition",F1Hidden)!;
            prepare.Invoke(_entities,[_player]); F1Enemy(native);
            string context=$"boss-init${source.Id:x2}/batch{batched}/fullParts{fullParts}";
            FailIf(native.RandomCalls != 1 || _random.CaptureState().Calls-seed.Calls != 1 ||
                _runtimeState.ReadField("wLinkForceState") != 0x0b || _runtimeState.ReadField("wLinkStateParameter") != 0x16 ||
                _player.FacingVector != Vector2I.Right || _player.PrecisePosition != new Vector2(16,160),
                context+": state0 must consume one native common RNG draw and publish its request before Link moves.");
            FailIf(!SomariaPrivate<bool>(owner.Node,"_initialized") || native[0xd084] != (source.Id == 0x78 ? 1 : 8),
                context+": source state0 must finish before scrolling presentation is exposed.");
            if (source.Id == 0x79)
                FailIf(!owner.Node.Visible || SomariaPrivate<int>(owner.Node,"_counter") != native[0xd086],
                    context+": Head Thwomp must publish its solid visible body and native counter18 in state0.");
            else FailIf(owner.Node.Visible,context+": native initial body/spawner must remain hidden.");
            if (source.Id == 0x72)
                FailIf(SomariaPrivate<int>(owner.Node,"_counter2") != native[0xd087],context+": Subterror must retain state0 counter2=30.");
            if (source.Id == 0x71)
                FailIf(_entities.Entities<BossShadowEffect>().Count != F1Count(native,7) || F1Count(native,7) != (fullParts ? 0 : 1),
                    context+": Swoop must attempt its one optional source state0 shadow before state8.");
            void Check()
            {
                foreach (string field in new[] { "wLinkForceState","wLinkStateParameter","wcc93","wDisableLinkCollisionsAndMenu" })
                    FailIf(_runtimeState.ReadField(field) != native[_runtimeState.FieldAddress(field)],context+$": {field} differs.");
                FailIf(_entities.NativeActiveMusicSource() != native[_runtimeState.FieldAddress("wActiveMusic")] ||
                    _player.PrecisePosition != new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                    _player.NativeForcedMovementActive != (native[0xd004] == 0x0b),context+": native sound gate/Link handoff differs.");
                FailIf(RandomDiffers(seed,native[0xff94],native[0xff95],native.RandomCalls) ||
                    !sounds.Requests.SequenceEqual(native.Sounds),context+": ordered native RNG/cues differ.");
            }
            Check(); prepare.Invoke(_entities,[_player]); Check(); // No replay while initialized bodies remain frozen.
            if (fullParts)
            {
                foreach (var part in F1Parts.Keys.ToArray()) _entities.RetireEntity(part);
                for (int index=0;index<16;index++) native[0xd0c0+index*256]=0;
            }
            _runtimeState.SetField("wScrollMode",1); native[_runtimeState.FieldAddress("wScrollMode")]=1;
            _runtimeState.SetField("wDisabledObjects",4); native[0xcc8a]=4; // Native enemy-category gate; parts still update.
            native.HostileEnemiesEnabled=true; native.HostilePartsEnabled=true; native.NativeGameKeysEnabled=true;
            StepGameplayUpdates(4,Vector2.Zero,[],[],batched,afterUpdate: () =>
            { native.UpdateGameplay(0,0,0xff,_entities.FrameCounter); Check(); });
            FailIf(_player.PrecisePosition != new Vector2(19,160),context+": deferred request must consume, then move on three selected Link visits.");
            if (source.Id == 0x71 && fullParts)
                FailIf(_entities.Entities<BossShadowEffect>().Count != 0 || F1Count(native,7) != 0,
                    context+": released capacity must not replay Swoop's optional state0 shadow.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared five native boss initializers/preload, one common RNG draw, header/request ownership, Swoop shadow capacity and actual Link handoff in both schedules.");
    }
}
