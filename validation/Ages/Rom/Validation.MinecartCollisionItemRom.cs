
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartCollisionItemRom()
    {
        int cases = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        foreach (int page in new[] { 0xd7, 0xdb })
        foreach (var gate in new (int Text, int Mask, int Palette, int Scroll)[] { (0,0,0,0), (1,0,0,0), (0,0x10,0,0),
            (0,0x80,0,0), (0,0,1,0), (0,0,0,8), (0,0x20,0,0), (0,0,0,2) })
        foreach (var parent in new (int Enabled, int Id)[] { (3,0x0a), (0,0x0a), (3,0x0b), (0,0) })
        {
            var memory = new OracleRuntimeState(game);
            var data = MinecartData.For(OracleAssetPack.ForGame(game));
            var native = new LinkCollisionRom(bankedStack:true, game:game);
            int at = page << 8;
            for (int offset = 0; offset < 0x40; offset++)
                memory.SetWramByte(at + offset, native[at + offset] = (byte)(offset + 0x40));
            memory.SetWramByte(at, native[at] = 0); memory.SetWramByte(at + 4, native[at + 4] = 0);
            for (int occupied = 0xd7; occupied < page; occupied++) native[occupied << 8] = 1;
            native[0xd100] = 3; native[0xd101] = 0x0a;
            // Execute the real producer before checking the untouched birth bytes.
            native.Call(game == OracleGame.Ages ? 0x57dd : 0x5727, bank:6, objectPage:0xd1);
            var item = new MinecartCollisionRoomEntity(memory, data);
            item.BindNativeItemSlot(page);
            string context = $"{game} ITEM $1d slot${page:x2}, gate={gate}, D1={parent}";
            Compare();
            for (int occupied = 0xd7; occupied < page; occupied++) native[occupied << 8] = 0;
            native[0xcba0] = (byte)gate.Text; native[0xc4ab] = (byte)gate.Palette;
            native[game == OracleGame.Ages ? 0xcc8a : 0xcca4] = (byte)gate.Mask;
            native[0xcd00] = (byte)gate.Scroll;
            var frame = new RoomEntityFrame(_player, 0, false, null);
            item.UpdateFrame(frame, new List<RoomEntitySpawn>());
            native.Call(game == OracleGame.Ages ? 0x4872 : 0x485a, bank:7);
            Compare();
            // Independent source literals, including damage's signed byte.
            FailIf(memory.ReadWramByte(at) != 3 || memory.ReadWramByte(at + 4) != 1 ||
                memory.ReadWramByte(at + 0x24) != 0x84 || memory.ReadWramByte(at + 0x26) != 6 ||
                memory.ReadWramByte(at + 0x27) != 6 || memory.ReadWramByte(at + 0x28) != 0xf8 ||
                memory.ReadWramByte(at + 0x29) != 0x7e || memory.ReadWramByte(at + 0x3a) != 0xf8 ||
                memory.ReadWramByte(at + 0x3c) != 0xff, context + ": state-zero attributes must initialize through every freeze gate.");
            if (gate.Text == 0 && (gate.Mask & 0x90) == 0 && gate.Palette == 0 && gate.Scroll != 8)
                item.UpdateFrame(frame, new List<RoomEntitySpawn>());
            native.Call(game == OracleGame.Ages ? 0x4872 : 0x485a, bank:7);
            Compare();
            memory.SetWramByte(0xd100, native[0xd100] = (byte)parent.Enabled);
            memory.SetWramByte(0xd101, native[0xd101] = (byte)parent.Id);
            byte[] position = [0x31,0x48,0x65,0x58,0x43,0xf9];
            for (int offset = 0; offset < position.Length; offset++)
                memory.SetWramByte(0xd10a + offset, native[0xd10a + offset] = position[offset]);
            item.UpdateItemPost();
            native.Call(game == OracleGame.Ages ? 0x491a : 0x4902, bank:7);
            Compare();
            FailIf(item.Finished != (parent.Id != 0x0a) || native.RandomUpdates != 0,
                context + ": unconditional post checks only D1.id and must consume no RNG.");
            cases++;
            void Compare()
            {
                for (int offset = 0; offset < 0x40; offset++)
                    FailIf(memory.ReadWramByte(at + offset) != native[at + offset],
                        $"{context}: byte${at + offset:x4}: port${memory.ReadWramByte(at + offset):x2}, native${native[at + offset]:x2}.");
            }
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var memory = _runtimeState;
            ResetMinecartsForValidation(memory, []);
            MinecartValidationView.PrepareMountedBody(memory, new(88,72), 1);
            _entities.AddEntity(new MinecartRoomEntity(new ActiveMinecart(-1,0,72,88,1,true),
                _currentRoom, memory, new DungeonInteractionVisualDatabase(AgesGameContent.AssetRoot).Visual("minecart"),
                _sound.PlaySound, _entities));
            _player.WarpTo(new(88.25f,72.5f)); _player.FinishMinecartMount(new(88,72),1,0);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom,1,88,72) { CompanionDispatchEnabled=true };
            native[0xd00a]=0x80; native[0xd00c]=0x40; native.InitializeLinkGameplay();
            native[0xd004]=0; native[0xd009]=0;
            native[0xd100]=3; native[0xd101]=0x0a; native[0xd108]=1; native[0xd109]=8;
            native[0xd10b]=72; native[0xd10d]=88;
            var occupants = new MinecartCollisionRoomEntity[5];
            for (int index=0; index<5; index++)
            {
                occupants[index] = _entities.TryCreateNativeItem(() => new MinecartCollisionRoomEntity(memory,
                    MinecartData.For(AgesGameContent.AssetRoot)))!;
                int at=(0xd7+index)<<8; native[at]=1; native[at+1]=0x1d;
            }
            _dialogue.ShowMessage("Item pool freeze.",_player.Position.Y); native[0xcba0]=1;
            Step(2);
            FailIf(memory.ReadWramByte(0xd136)!=0 || native[0xd136]!=0,
                "A full native item pool must not mark the cart's collision child as created.");
            _entities.RetireEntity(occupants[2]);
            for (int offset=0; offset<0x40; offset++) native[0xd900+offset]=0;
            Step(1);
            FailIf(memory.ReadWramByte(0xd136)!=1 || native[0xd136]!=1 || memory.ReadWramByte(0xd901)!=0x1d,
                "Frozen SPECIALOBJECT $0a must retry and initialize ITEM $1d in the newly free $d9 slot.");
            _entities.ClearPhysicalPlayerItems(); native.ClearPhysicalItems();
            Step(2);
            FailIf(memory.ReadWramByte(0xd136)!=1 || _entities.EntityAdapters<MinecartCollisionRoomEntity>().Any(),
                "Clearing ITEMS must retain var36=$01; the source never repairs that retained creation flag.");
            _dialogue.Close();
            void Step(int count)
            {
                StepGameplayUpdates(count,Vector2.Zero,[],[],batched,() =>
                {
                    native.UpdateGameplay(0,0,0xff,_saveData.ReadField("wPlaytimeCounter"));
                    FailIf(memory.ReadWramByte(0xd136)!=native[0xd136],"Native cart child-allocation flag differs.");
                    for (int page=0xd7; page<=0xdb; page++)
                    for (int offset=0; offset<0x40; offset++)
                    {
                        int at=(page<<8)+offset;
                        FailIf(memory.ReadWramByte(at)!=native[at],$"Cart ITEM pool byte${at:x4}: port${memory.ReadWramByte(at):x2}, native${native[at]:x2}, batch={batched}.");
                    }
                    FailIf(RandomDiffers(seed,native[0xff94],native[0xff95],native.RandomCalls),"Native cart allocation changed RNG.");
                });
            }
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x33); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int y=8; y<128; y+=16)
            for (int x=8; x<160; x+=16) _currentRoom.SetPositionTileAndCollision(new(x,y),0xa0,0,0);
            _player.WarpTo(new(24,24)); _player.BeginCutsceneControl();
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData,seed,_currentRoom,0,24,24);
            var adapter = CreateOctorokInitializationActor(0);
            var actor = (OctorokCharacter)adapter.Node;
            _entities.RegisterEnemySlot(adapter,0); _entities.AddEntity(adapter);
            native[0xd080]=1; native[0xd081]=9;
            native.Word(0xd08a,0x4880); native.Word(0xd08c,0x5040);
            StepGameplayUpdates(1,Vector2.Zero,batched:batched,
                afterUpdate:() => native.AdvanceEnemies(_entities.FrameCounter));
            // Declared health isolates repeated damage from death progression.
            actor.Health = 0x20; native[0xd0a9]=0x20;
            _runtimeState.SetWramByte(0xd101,0x0a);
            var item = _entities.TryCreateNativeItem(() => new MinecartCollisionRoomEntity(_runtimeState,
                MinecartData.For(AgesGameContent.AssetRoot)))!;
            native[0xd700]=3; native[0xd701]=0x1d;
            var sounds = _sound.AttachPlayRequestAudit();
            for (int update=0; update<52; update++)
            {
                Vector2 origin = actor.Position.Floor() + Vector2.Right*4;
                _runtimeState.SetWramByte(0xd10b,(byte)origin.Y);
                _runtimeState.SetWramByte(0xd10d,(byte)origin.X);
                StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    native.AdvanceEnemies(_entities.FrameCounter);
                    native.HitEnemyWithItem(0,4,8,itemId:0x1d,origin:origin);
                    string context=$"Minecart ITEM $1d combat update{update}, batch={batched}";
                    FailIf(actor.Health!=native[0xd0a9] || actor.InvincibilityCounter!=native[0xd0ab] ||
                        actor.KnockbackCounter!=native[0xd0ad] || actor.NativeHitPending!=((native[0xd0aa]&0x80)!=0) ||
                        _runtimeState.ReadWramByte(item.Address+0x2a)!=native[0xd72a],
                        $"{context}: health {actor.Health}/${native[0xd0a9]:x2}, invincibility {actor.InvincibilityCounter}/${native[0xd0ab]:x2}, recoil {actor.KnockbackCounter}/${native[0xd0ad]:x2}, hit={actor.NativeHitPending}/${native[0xd0aa]:x2}, flags=${_runtimeState.ReadWramByte(item.Address+0x2a):x2}/${native[0xd72a]:x2}.");
                    FailIf(update==0 && actor.Health!=0x18,context+": minecart damage must remain eight health units independently of Link's unequipped sword.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds) ||
                        RandomDiffers(seed,native[0xff94],native[0xff95],native.RandomCalls),context+": cues/RNG differ.");
                });
            }
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _currentRoom.SetPositionTileAndCollision(new(88,72),0x5d,0,0);
            _currentRoom.SetPositionTileAndCollision(new(104,72),0x5f,0,0);
            ResetMinecartsForValidation(_runtimeState,[]);
            MinecartValidationView.PrepareMountedBody(_runtimeState,new(88,72),1);
            _runtimeState.SetWramByte(0xd109,8);
            _entities.AddEntity(new MinecartRoomEntity(new ActiveMinecart(-1,0,72,88,1,true),
                _currentRoom,_runtimeState,new DungeonInteractionVisualDatabase(AgesGameContent.AssetRoot).Visual("minecart"),
                _sound.PlaySound,_entities));
            _player.WarpTo(new(88.25f,72.5f)); _player.FinishMinecartMount(new(88,72),1,0);
            var seed=_random.CaptureState();
            var native=new SomariaRom(_saveData,seed,_currentRoom,1,88,72) { CompanionDispatchEnabled=true };
            native[0xd00a]=0x80; native[0xd00c]=0x40; native.InitializeLinkGameplay();
            native[0xd004]=0; native[0xd009]=0;
            native[0xd100]=3; native[0xd101]=0x0a; native[0xd108]=1; native[0xd109]=8;
            native[0xd10b]=72; native[0xd10d]=88;
            // Real inert e6:02 parents occupy all fourteen interaction slots.
            // Initialized handlers freeze under $02; state zero still runs.
            for (int slot=2;slot<16;slot++)
            {
                _entities.TryCreateInteractionEntity(new RaftInteractionSpawn(new(200,120),4,0,SubId:2));
                int at=0xd040+slot*256;
                native[at]=1; native[at+1]=0xe6; native[at+2]=2; native[at+0xb]=120; native[at+0xd]=200;
            }
            _runtimeState.SetField("wDisabledObjects",2); native[0xcc8a]=2;
            Step(1);
            FailIf(_entities.InteractionSlotAvailable || native[0xd104]!=1,
                "Cart failure fixture requires initialized D1 and fourteen retained interactions before dismount.");
            Step(1);
            FailIf(_entities.EntityAdapters<MinecartInteractionRoomEntity>().Any() ||
                _entities.EntityAdapters<MinecartRoomEntity>().Any(cart=>!cart.Finished) ||
                _entities.EntityAdapters<MinecartCollisionRoomEntity>().Any(item=>!item.Finished) ||
                _runtimeState.ReadField("wLinkObjectIndex")!=0xd0 || native[0xcc2c]!=0xd0,
                "A failed dismount allocation must delete D1 and its collision item without creating a stationary cart or retaining a retry state.");
            var released=_entities.EntityAdapters<RaftInteractionRoomEntity>().First();
            int releasedAt=0xd040+_entities.NativeInteractionSlot(released)*256;
            _entities.RetireEntity(released);
            for (int offset=0;offset<0x40;offset++) native[releasedAt+offset]=0;
            Step(3);
            FailIf(_entities.EntityAdapters<MinecartInteractionRoomEntity>().Any() || !_entities.InteractionSlotAvailable,
                "Freeing interaction capacity after failed cart dismount must not resurrect or retry the deleted D1 parent.");
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,[],[],batched,() =>
            {
                native.UpdateGameplay(0,0,0xff,_saveData.ReadField("wPlaytimeCounter"));
                for (int address=0xd100;address<0xd140;address++)
                    FailIf(_runtimeState.ReadWramByte(address)!=native[address],$"Full-pool cart D1 byte${address:x4} differs, batch={batched}.");
                for (int address=0xd700;address<0xd740;address++)
                    FailIf(_runtimeState.ReadWramByte(address)!=native[address],$"Full-pool cart ITEM byte${address:x4} differs, batch={batched}.");
                for (int address=0xcd80;address<0xcdc0;address++)
                    FailIf(_runtimeState.ReadWramByte(address)!=native[address],"Failed cart allocation must not save a phantom static row.");
                FailIf(RandomDiffers(seed,native[0xff94],native[0xff95],native.RandomCalls),"Failed cart handoff changed RNG.");
            });
        }
        GD.Print($"Validated {cases} both-ROM ITEM $1d birth/init/post cases, real-loop item-pool retry, interaction-pool dismount failure, and repeated native combat/status handoffs under both host schedules.");
    }
}
