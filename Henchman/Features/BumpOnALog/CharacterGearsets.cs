using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Henchman.Features.BumpOnALog;

internal static class CharacterGearsets
{
    internal readonly record struct Gearset(byte Id, string Name, byte ClassJob);

    internal static List<Gearset> Parse(ReadOnlySpan<byte> file)
    {
        // GEARSET.DAT v109/110: 16-byte header, plain 0xFF marker, XOR-0x73 data
        // (4-byte state prefix, 101 entries of 0x1C4 bytes), then a 16-byte footer.
        // Entry 100 is an internal equipment snapshot, not a selectable gearset.
        var entrySize = Unsafe.SizeOf<RaptureGearsetModule.GearsetEntry>();
        var dataSize = 5 + 101 * entrySize;
        if (file.Length < dataSize + 32 ||
            BinaryPrimitives.ReadUInt16LittleEndian(file) != 5 ||
            BinaryPrimitives.ReadUInt16LittleEndian(file[2..]) is not (109 or 110) ||
            BinaryPrimitives.ReadUInt32LittleEndian(file[4..]) != dataSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(file[8..]) != dataSize)
            throw new InvalidDataException("Unsupported GEARSET.DAT format.");

        var result = new List<Gearset>();
        Span<byte> decoded = stackalloc byte[entrySize];
        for (var i = 0; i < 100; i++)
        {
            var encoded = file.Slice(21 + i * entrySize, entrySize);
            for (var n = 0; n < entrySize; n++) decoded[n] = (byte)(encoded[n] ^ 0x73);
            var entry = MemoryMarshal.Read<RaptureGearsetModule.GearsetEntry>(decoded);
            if (!entry.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (entry.Id >= 100) throw new InvalidDataException("Invalid gearset ID.");
            result.Add(new Gearset(entry.Id, entry.NameString, entry.ClassJob));
        }

        return result;
    }
}
