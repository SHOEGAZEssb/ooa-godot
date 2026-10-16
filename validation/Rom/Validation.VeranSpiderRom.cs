using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranSpiderRom()
    {
        foreach (bool batched in new[] {false,true})
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xfc); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(0x78,0x98));
            FailIf(_collision.Collides(_player.Position),"Veran spider fixture requires source room $4:fc floor.");
            var spider=(VeranSpiderRoomEntity)_entities.TryCreateVeranSpider(new(0x78,0x58))!;
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x98) {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay(); rom[0xd080]=1; rom[0xd081]=0x0f; rom[0xd082]=1; rom[0xd08b]=0x58; rom[0xd08d]=0x78; rom[0xcdd1]=1;
            int update=0; bool frozen=true, landed=false, charged=false;
            _entities.TextActiveSource=()=>frozen;
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom[0xcba0]=(byte)(frozen?1:0); rom.UpdateGameplay(0,0,0xff,++update);
                var actor=spider.Actor;
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) || _player.KnockbackFrames!=rom[0xd02d] ||
                    _player.InvincibilityFrames!=unchecked((sbyte)rom[0xd02b]),
                    $"Veran spider Link update={update}: XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, recoil={_player.KnockbackFrames}/{rom[0xd02d]}, inv={_player.InvincibilityFrames}/{rom[0xd02b]}, native angle={rom[0xd02c]:x2}.");
                FailIf(actor.State!=rom[0xd084] || actor.Counter!=rom[0xd086] || actor.Cooldown!=rom[0xd087] || actor.Angle!=rom[0xd089] ||
                    actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f) || actor.ZFixed!=unchecked((short)rom.Word(0xd08e)) ||
                    actor.Speed!=rom[0xd090] || actor.SpeedZ!=unchecked((short)rom.Word(0xd094)) || actor.ScentCounter!=rom[0xd0bd],
                    $"Veran spider $0f:$01 update={update}, batch={batched}: port={actor.State}/{actor.Counter}/{actor.Cooldown}/{actor.Angle}/{actor.Position}/{actor.ZFixed}/{actor.Speed}/{actor.SpeedZ}/{actor.ScentCounter}; " +
                    $"ROM={rom[0xd084]}/{rom[0xd086]}/{rom[0xd087]}/{rom[0xd089]}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}/{unchecked((short)rom.Word(0xd08e))}/{rom[0xd090]}/{unchecked((short)rom.Word(0xd094))}/{rom[0xd0bd]}.");
                var random=_random.CaptureState();
                FailIf(random.Rng1!=rom[0xff94] || random.Rng2!=rom[0xff95] || random.Calls-seed.Calls!=rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran spider shared RNG/sound order differs update {update}: RNG={random.Rng1:x2}/{random.Rng2:x2}/{random.Calls-seed.Calls} vs {rom[0xff94]:x2}/{rom[0xff95]:x2}/{rom.RandomCalls}; " +
                    $"sounds={string.Join(',',sounds.Requests)} vs {string.Join(',',rom.Sounds)}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, health={_player.HealthQuarters}/{rom[0xc6aa]}.");
                landed|=actor.State==9; charged|=actor.State==10;
            }
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(3); frozen=false; Step(320); frozen=true; Step(4); frozen=false; Step(180);
            FailIf(!landed || !charged,"Veran spider trace must exercise random falling placement, landing, diagonal charge and wall turns.");
            _runtimeState.SetWramByte(0xcfd0,1); rom[0xcfd0]=1;
            StepGameplayUpdates(1,Vector2.Zero);
            rom.UpdateGameplay(0,0,0xff,++update);
            FailIf(!spider.Finished || rom[0xd080]!=0,"cfd0 must retire Veran spiders immediately through their normal death owner.");
        }
    }
}
