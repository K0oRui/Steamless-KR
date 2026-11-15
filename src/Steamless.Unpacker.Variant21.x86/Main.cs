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

namespace Steamless.Unpacker.Variant21.x86
{
    using API;
    using API.Model;
    using API.PE;
    using Classes;
    using Common;
    using System;
    using System.Collections.Generic;

    [SteamlessApiVersion(2, 0)]
    public class Main : StubUnpacker<V21Run>
    {
        private const string StubPrologue = "53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 C7";

        private const uint HeaderMagic = 0xC0DEC0DE;

        protected override string VariantName => "SteamStub Variant 2.1";

        public override string Name => "SteamStub Variant 2.1 Unpacker (x86)";

        public override string Description => "Unpacks 32-bit SteamStub variant 2.1.";

        protected override string BindPrologue => StubPrologue;

        protected override bool ReadHeader(V21Run run)
        {
            this.Step(run, "Disassemble and decode the SteamStub header.");

            // The stub header magic marks the start of the DRM header before the entry point.
            if (!HasHeaderMagic(run.File, HeaderMagic))
                return false;

            if (!StubDisassembler.TryFindV21Header(run.File, out var headerOffset, out var headerSize, out var headerXorKey))
                return false;

            if (!TryReadHeaderWindow(run.File, headerOffset, headerSize, out var headerData))
                return false;

            run.XorKey = StubCrypto.Xor(ref headerData, (uint)headerData.Length, headerXorKey);

            // The 0xD0 variant of this header omits the LoadLibraryW pointer, so it is 4 bytes shorter.
            if ((headerSize / 4) == 0xD0)
                run.Header = PeHelpers.GetStructure<SteamStub32Var21Header_D0Variant>(headerData);
            else
                run.Header = PeHelpers.GetStructure<SteamStub32Var21Header>(headerData);

            return true;
        }

        protected override bool Unpack(V21Run run, SteamlessOptions options)
        {
            this.Step(run, "Decrypt the payload.");
            var payloadRva = run.File.RvaFromVa(run.Header.PayloadDataVirtualAddress);
            if (!Stub3xFlow.TryDecryptPayload(run.File, payloadRva, run.Header.PayloadDataSize, run.XorKey, PayloadSizing.Declared,
                                              options, this.LoggingService, out var payload))
                return false;

            run.Payload = payload;

            this.Step(run, "Decrypt and dump SteamDRMP.dll.");
            if (!Stub21Flow.TryDecryptDrmp(run.File, run.Payload, run.Header.SteamDRMPDllVirtualAddress, run.Header.SteamDRMPDllSize,
                                           run.Header.XTeaKeys, options, this.LoggingService, out var drmp))
                return false;

            run.SteamDrmp = drmp;

            this.Step(run, "Pull the offsets this stub needs out of SteamDRMP.dll.");
            if (!Stub21Offsets.TryRead(run.Payload, run.SteamDrmp, this.LoggingService, out var offsets))
                return false;

            run.SteamDrmpOffsets = offsets;

            this.Step(run, "Decrypt the main code section.");
            return Stub21Flow.TryPrepareCodeSection(run.File, run.Payload, run.SteamDrmpOffsets, run.Bind, options, this.LoggingService);
        }

        protected override uint GetEntryPoint(V21Run run)
        {
            return (uint)run.File.RvaFromVa(BitConverter.ToUInt32(run.Payload, run.SteamDrmpOffsets[2]));
        }
    }

    /// <summary>
    /// Run state for a 2.1 unpack. Everything after the header lives in the payload, so the payload,
    /// the decrypted SteamDRMP.dll and the offsets found in it travel together.
    /// </summary>
    public sealed class V21Run : UnpackRun
    {
        public ISteamStub32Var21Header Header { get; set; }

        public byte[] Payload { get; set; }

        public byte[] SteamDrmp { get; set; }

        public List<int> SteamDrmpOffsets { get; set; }
    }
}
