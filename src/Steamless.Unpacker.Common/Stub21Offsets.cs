#nullable disable

/**
 * Steamless - Copyright (c) 2015 - 2024 atom0s [atom0s@live.com]
 *
 * This work is licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-nd/4.0/ or send a letter to
 * Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
 *
 * By using Steamless, you agree to the above license and its terms.
 *
 *      Attribution - You must give appropriate credit, provide a link to the license and indicate if changes were
 *                    made. You must do so in any reasonable manner, but not in any way that suggests the licensor
 *                    endorses you or your use.
 *
 *   Non-Commercial - You may not use the material (Steamless) for commercial purposes.
 *
 *   No-Derivatives - If you remix, transform, or build upon the material (Steamless), you may not distribute the
 *                    modified material. You are, however, allowed to submit the modified works back to the original
 *                    Steamless project in attempt to have it added to the original project.
 *
 * You may not apply legal terms or technological measures that legally restrict others
 * from doing anything the license permits.
 *
 * No warranties are given.
 */

namespace Steamless.Unpacker.Common
{
    using API.Events;
    using API.PE;
    using API.Services;
    using Iced.Intel;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The 2.1 stub keeps its offsets in the decrypted payload, not its own header, because the
    /// stub loaded them into SteamDRMP.dll. Finding them takes four strategies in order: a known
    /// instruction pattern with fixed displacements, the same pattern disassembled, a brute-force
    /// scan, and then the next pattern.
    /// </summary>
    public static class Stub21Offsets
    {
        private const int AesIvSize = 16;

        /// <summary>How many offsets the stub loads, in the order it loads them.</summary>
        public const int Count = 8;

        private static readonly string[] Patterns =
        {
            "8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8D ?? ?? ?? ?? ?? 05",
            "8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B",
            "8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B"
        };

        /// <summary>
        /// Finds the eight offsets the stub reads out of the payload. Every value here is attacker
        /// controlled, so each one is bounds checked against the payload.
        /// </summary>
        public static bool TryRead(byte[] payload, byte[] drmp, LoggingService logService, out List<int> offsets)
        {
            offsets = null;

            foreach (var pattern in Patterns)
            {
                var drmpOffset = PeHelpers.FindPattern(drmp, pattern);
                if (drmpOffset == -1)
                    continue;

                var copySize = Math.Min(1024, drmp.Length - (int)drmpOffset);
                if (copySize < 76)
                    continue;

                var block = new byte[copySize];
                Array.Copy(drmp, drmpOffset, block, 0, copySize);

                foreach (var useFallback in new[] { false, true })
                {
                    var fixedOffsets = ReadAtFixedPositions(block, useFallback);
                    if (fixedOffsets.Count == Count && Validate(payload, fixedOffsets))
                    {
                        offsets = fixedOffsets;
                        return true;
                    }
                }

                // The fixed positions do not always hold the values; fall back to the disassembler.
                var dynamicOffsets = ReadByDisassembly(block);
                if (dynamicOffsets.Count == Count && Validate(payload, dynamicOffsets))
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Extracted the offsets dynamically.", LogMessageType.Debug));
                    offsets = dynamicOffsets;
                    return true;
                }

                // Last resort for this pattern: scan the data for a valid layout.
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Scanning the DRMP data block for an offset layout...", LogMessageType.Debug));
                var scannedOffsets = Scan(payload, drmp, drmpOffset, logService);
                if (scannedOffsets != null)
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Scan found valid offsets.", LogMessageType.Debug));
                    offsets = scannedOffsets;
                    return true;
                }

                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Pattern matched, but no valid offsets. Trying the next pattern.", LogMessageType.Debug));
            }

            return false;
        }

        /// <summary>
        /// Reads the values in the order the stub loads them: flags, app id, OEP, code section,
        /// encrypted size, AES key, then the IV pair derived from one displacement.
        /// </summary>
        private static List<int> ReadAtFixedPositions(byte[] data, bool useFallback)
        {
            var offset0 = 2; // Flags
            var offset1 = 14; // Steam App Id
            var offset2 = useFallback ? 25 : 26; // OEP
            var offset3 = useFallback ? 36 : 38; // Code Section Virtual Address
            var offset4 = useFallback ? 47 : 50; // Code Section Virtual Size (Encrypted Size)
            var offset5 = useFallback ? 61 : 62; // Code Section AES Key
            var offset6 = useFallback ? 72 : 67; // Code Section AES Iv

            var offsets = new List<int>
                {
                    BitConverter.ToInt32(data, offset0),
                    BitConverter.ToInt32(data, offset1),
                    BitConverter.ToInt32(data, offset2),
                    BitConverter.ToInt32(data, offset3),
                    BitConverter.ToInt32(data, offset4),
                    BitConverter.ToInt32(data, offset5)
                };

            var aesIvOffset = BitConverter.ToInt32(data, offset6);
            offsets.Add(aesIvOffset);
            offsets.Add(aesIvOffset + AesIvSize);

            return offsets;
        }

        /// <summary>
        /// Reads the offset values in the order the stub loads them: mov for the scalars, lea for
        /// the AES key, add for the stolen block. The trailing add can follow a mov that loads
        /// GetModuleHandleA, so one mov is skipped after the lea.
        /// </summary>
        private static List<int> ReadByDisassembly(byte[] data)
        {
            var offsets = new List<int>();
            var count = 0;

            try
            {
                var skipMov = false;
                var reader = new ByteArrayCodeReader(data);
                var decoder = Decoder.Create(32, reader);
                var endRip = decoder.IP + (uint)data.Length;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    if (count >= Count)
                        break;

                    var instruction = decoder.Decode();

                    if (!skipMov && instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Memory)
                    {
                        count++;
                        offsets.Add((int)instruction.MemoryDisplacement32);
                    }

                    if (instruction.Code == Code.Lea_r32_m &&
                        instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Memory)
                    {
                        count += 2;
                        offsets.Add((int)instruction.MemoryDisplacement32);
                        offsets.Add((int)instruction.MemoryDisplacement32 + AesIvSize);

                        skipMov = true;
                    }

                    if (instruction.Mnemonic == Mnemonic.Add &&
                        instruction.Op0Kind == OpKind.Register && StubDisassembler.IsImmediate32(instruction.Op1Kind))
                    {
                        count++;
                        offsets.Add((int)instruction.Immediate32);
                    }
                }

                return offsets;
            }
            catch (Exception)
            {
                return new List<int>();
            }
        }

        private static bool Validate(byte[] payload, List<int> offsets)
        {
            if (offsets.Count != Count)
                return false;

            // The offsets come from the payload itself. Use long arithmetic so a value near
            // int.MaxValue cannot overflow the bounds checks.
            if (!PeHelpers.IsValidRange(payload, offsets[0], 4))
                return false;

            // Validate the OEP and code section addresses, which later steps consume.
            if (!PeHelpers.IsValidRange(payload, offsets[2], 4))
                return false;
            if (!PeHelpers.IsValidRange(payload, offsets[3], 4))
                return false;

            var flags = BitConverter.ToUInt32(payload, offsets[0]);
            if (StubDrmFlags.EncryptsCodeSectionV21(flags))
            {
                // File is encrypted; validate the encryption-related offsets.
                if (!PeHelpers.IsValidRange(payload, offsets[4], 4))
                    return false;
                if (!PeHelpers.IsValidRange(payload, offsets[5], 32))
                    return false;
                if (!PeHelpers.IsValidRange(payload, offsets[6], 16))
                    return false;
                if (!PeHelpers.IsValidRange(payload, offsets[7], 16))
                    return false;
            }

            return true;
        }

        private static List<int> Scan(byte[] payload, byte[] drmp, long scanOffset, LoggingService logService)
        {
            var copySize = Math.Min(1024, drmp.Length - (int)scanOffset);
            if (copySize <= 0)
                return null;

            var data = new byte[copySize];
            Array.Copy(drmp, scanOffset, data, 0, copySize);

            for (var start = 0; start < data.Length - 28; start += 2)
            {
                var values = new List<int>();
                for (var j = 0; j < 6; j++)
                    values.Add(BitConverter.ToInt32(data, start + (j * 4)));

                // values[6] is the IV offset, which also gives the stolen-block offset.
                var ivOffset = BitConverter.ToInt32(data, start + (6 * 4));
                values.Add(ivOffset);
                values.Add(ivOffset + AesIvSize);

                if (!PeHelpers.IsValidRange(payload, values[0], 4))
                    continue;

                var flags = BitConverter.ToUInt32(payload, values[0]);
                var encrypted = StubDrmFlags.EncryptsCodeSectionV21(flags);

                if (!PeHelpers.IsValidRange(payload, values[3], 4))
                    continue;

                if (encrypted)
                {
                    if (!PeHelpers.IsValidRange(payload, values[4], 4)) continue;
                    if (!PeHelpers.IsValidRange(payload, values[5], 32)) continue;
                    if (!PeHelpers.IsValidRange(payload, values[6], 16)) continue;
                    if (!PeHelpers.IsValidRange(payload, values[7], 16)) continue;

                    if ((long)values[5] + 32 > values[6]) continue;
                    if ((long)values[6] + 16 != values[7]) continue;
                }

                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Found a valid offset layout at byte {start}.", LogMessageType.Debug));
                return values;
            }

            return null;
        }
    }
}
