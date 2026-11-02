using System.Security.Cryptography;

namespace Oracles;

/// <summary>
/// Address-independent view of the original banks $39-$3e sound payload.
/// Pointers retain their Game Boy addresses; this class maps them into the
/// compact generated bank image imported from the selected game's verified ROM.
/// </summary>
public sealed partial class OracleSoundData
{
    public const int SoundCount = 0xdf;
    public const int MusicCount = 0x4c;
    public const int BaseBank = 0x39;
    public const int BankCount = 6;
    public const int BankSize = 0x4000;
    public const int WaveformCount = 0x2e;
    public const int FrequencyCount = 87;

    private readonly int _soundPointerTableAddress;
    private readonly byte[] _sound, _roomMusic, _waveforms, _noise, _frequencies, _envelopeDelays;
    private readonly ConditionalRoomMusicRule[] _conditionalRoomMusic;
    private readonly RoomFlagMusicRule[] _roomFlagMusic;
    internal OracleGame Game { get; }

    internal OracleSoundData(string assetRoot)
    {
        Game = OracleAssetPack.GameFor(assetRoot);
        bool seasons = Game == OracleGame.Seasons;
        _soundPointerTableAddress = seasons ? 0xe57cf : 0xe5748;
        _sound = LoadExact(assetRoot + "audio/sound_data.bin", BankCount * BankSize);
        _roomMusic = LoadExact(assetRoot + "audio/room_music.bin", 6 * 256);
        _waveforms = LoadExact(assetRoot + "audio/waveforms.bin", WaveformCount * 16);
        _noise = LoadExact(assetRoot + "audio/noise_frequencies.bin", 13 * 3);
        _frequencies = LoadExact(assetRoot + "audio/frequencies.bin", FrequencyCount * 2);
        _envelopeDelays = LoadExact(assetRoot + "audio/envelope_delays.bin", 16 * 8);
        _conditionalRoomMusic = seasons ? [] : LoadAgesConditionalRoomMusic();
        _roomFlagMusic = seasons ? [] : LoadAgesRoomFlagMusic();
        // Executable driver entries and WRAM layout are a clean-US contract.
        // Import-AudioData already retains this complete bank from the ROM
        // checked by the import entry point; never execute another revision.
        string digest = Convert.ToHexString(SHA256.HashData(_sound.AsSpan(0, BankSize)));
        if (digest != (seasons
            ? "E08707B10629F70455ABE68568D6074D2FD1268B2E519F5FE1A78177640A35D5"
            : "6BB47CDEF9823B11B924030F7731545457214A4B8292A20B5EC27D8A59EE4CF2"))
            throw new InvalidOperationException(
                "code/audio.s: imported sound driver bank $39 differs from the supported clean US ROM.");
    }

    public IReadOnlyList<ChannelStart> ChannelsFor(int soundId)
    {
        if ((uint)soundId >= SoundCount)
            throw new ArgumentOutOfRangeException(nameof(soundId));

        int pointerOffset = ToOffset(_soundPointerTableAddress) + soundId * 3;
        int bank = BaseBank + _sound[pointerOffset];
        int descriptor = PointerOffset(BaseBank, ReadWord(pointerOffset + 1));
        var channels = new List<ChannelStart>(4);
        for (int guard = 0; guard < 9; guard++)
        {
            int value = ReadByte(descriptor++);
            if (value == 0xff)
                return channels;
            int pointer = ReadWord(descriptor);
            descriptor += 2;
            channels.Add(new ChannelStart(
                value & 0x0f, (value >> 4) + 1, bank,
                PointerOffset(bank, pointer)));
        }
        throw new InvalidOperationException(
            $"Sound ${soundId:x2} channel descriptor did not terminate.");
    }

    public int RoomMusic(int group, int room)
    {
        if (group is 6 or 7)
            group -= 2;
        if ((uint)group >= 6 || (uint)room >= 256)
            throw new ArgumentOutOfRangeException(
                $"Music assignment group/room is outside $00-$05:$00-$ff: {group:x2}:{room:x2}.");
        return _roomMusic[group * 256 + room];
    }

    public int RoomMusic(int group, int room, OracleSaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Game != Game) throw new ArgumentException("Room music and save must belong to the same game.", nameof(save));
        int assignedMusic = RoomMusic(group, room);
        foreach (ConditionalRoomMusicRule rule in _conditionalRoomMusic)
        {
            if (rule.Group == group && rule.Room == room &&
                save.HasGlobalFlag(rule.RequiredGlobalFlag) &&
                !save.HasRoomFlag(group, room, rule.ClearRoomFlagMask))
            {
                assignedMusic = rule.Music;
                break;
            }
        }
        foreach (RoomFlagMusicRule rule in _roomFlagMusic)
        {
            if (rule.Group == group && rule.AssignedMusic == assignedMusic &&
                !save.HasRoomFlag(rule.FlagGroup, rule.FlagRoom, rule.ClearFlagMask))
                return rule.Music;
        }
        return assignedMusic;
    }

    public int ReadByte(int offset)
    {
        if ((uint)offset >= _sound.Length)
            throw new InvalidOperationException($"Sound read escaped imported banks at ${offset:x5}.");
        return _sound[offset];
    }

    public int ReadWord(int offset) => ReadByte(offset) | (ReadByte(offset + 1) << 8);

    public int PointerOffset(int bank, int pointer)
    {
        if (bank < BaseBank || bank >= BaseBank + BankCount)
            throw new InvalidOperationException($"Sound bank ${bank:x2} is outside $39-$3e.");
        return (bank - BaseBank) * BankSize + (pointer & 0x3fff);
    }

    public ushort FrequencyRegister(int note)
    {
        int index = note - 0x0c;
        return FrequencyRegisterByIndex(index);
    }

    public ushort FrequencyRegisterByIndex(int index)
    {
        if ((uint)index >= FrequencyCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"soundFrequencyTable: note index ${index:x2} is outside the named table.");
        return (ushort)(_frequencies[index * 2] | (_frequencies[index * 2 + 1] << 8));
    }

    public int EnvelopeAttackFrames(int volume, int envelope)
    {
        if ((uint)volume >= 16 || (uint)envelope >= 8)
            throw new ArgumentOutOfRangeException(
                $"Envelope delay index is outside volume $00-$0d / envelope $00-$07: " +
                $"${volume:x2}:${envelope:x2}.");
        return _envelopeDelays[volume * 8 + envelope];
    }

    public int VibratoOffset(int phase)
    {
        if ((uint)phase >= 8)
            throw new ArgumentOutOfRangeException(nameof(phase));
        int offset = 112 + phase * 2;
        return (short)(_envelopeDelays[offset] | (_envelopeDelays[offset + 1] << 8));
    }

    public float WaveSample(int waveform, int sample)
    {
        if ((uint)waveform >= WaveformCount)
            throw new ArgumentOutOfRangeException(nameof(waveform),
                $"audio/common/waveforms.s: waveform ${waveform:x2} is outside $00-$2d.");
        int packed = _waveforms[waveform * 16 + ((sample & 31) >> 1)];
        int value = (sample & 1) == 0 ? packed >> 4 : packed & 0x0f;
        return (value - 7.5f) / 7.5f;
    }

    public bool TryGetNoise(int note, out NoiseRecord record)
    {
        for (int offset = 0; offset < _noise.Length; offset += 3)
        {
            if (_noise[offset] != note)
                continue;
            record = new NoiseRecord(_noise[offset], _noise[offset + 1], _noise[offset + 2]);
            return true;
        }
        record = default;
        return false;
    }

    private static int ToOffset(int absoluteAddress) =>
        absoluteAddress - BaseBank * BankSize;

    private static byte[] LoadExact(string path, int expectedLength)
    {
        byte[] data = OracleAssetCache.ReadBytes(path);
        if (data.Length != expectedLength)
            throw new InvalidOperationException(
                $"Generated sound asset {path} has {data.Length} bytes; expected {expectedLength}.");
        return data;
    }

    private readonly record struct ConditionalRoomMusicRule(
        int Group,
        int Room,
        int Music,
        int RequiredGlobalFlag,
        byte ClearRoomFlagMask,
        string Source);

    private readonly record struct RoomFlagMusicRule(
        int Group, int AssignedMusic, int FlagGroup, int FlagRoom, byte ClearFlagMask, int Music, string Source);
}

public readonly record struct NoiseRecord(byte Note, byte Envelope, byte Frequency);

public readonly record struct ChannelStart(int Channel, int Priority, int Bank, int Offset);
