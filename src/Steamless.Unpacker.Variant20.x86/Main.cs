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

namespace Steamless.Unpacker.Variant20.x86
{
    using API;
    using API.Events;
    using API.Model;
    using API.PE;
    using Classes;
    using Common;
    using System;

    [SteamlessApiVersion(2, 0)]
    public class Main : StubUnpacker<V20Run>
    {
        private const string StubPrologue = "53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 BE";

        private const uint HeaderMagic = 0xC0DEC0DE;

        protected override string VariantName => "SteamStub Variant 2.0";

        public override string Name => "SteamStub Variant 2.0 Unpacker (x86)";

        public override string Description => "Unpacks 32-bit SteamStub variant 2.0.";

        protected override string BindPrologue => StubPrologue;

        protected override bool ReadHeader(V20Run run)
        {
            this.Step(run, "Disassemble and decode the SteamStub header.");

            // The stub header magic marks the start of the DRM header before the entry point.
            if (!HasHeaderMagic(run.File, HeaderMagic))
                return false;

            if (!StubDisassembler.TryFindV20Header(run.File, out var headerOffset, out var headerSize))
                return false;

            if (!TryReadHeaderWindow(run.File, headerOffset, headerSize, out var headerData))
                return false;

            run.XorKey = StubCrypto.Xor(ref headerData, (uint)headerData.Length);

            switch (headerSize)
            {
                case 856:
                    run.Header = PeHelpers.GetStructure<SteamStub32Var20_856_Header>(headerData);
                    break;
                case 884:
                    run.Header = PeHelpers.GetStructure<SteamStub32Var20_884_Header>(headerData);
                    break;
                case 952:
                    run.Header = PeHelpers.GetStructure<SteamStub32Var20_952_Header>(headerData);
                    break;
                default:
                    this.Log("", LogMessageType.Error);
                    this.Log($"Unrecognized variant header size: {headerSize}", LogMessageType.Error);
                    this.Log("Report this at https://github.com/K0oRui/Steamless-KR/issues", LogMessageType.Error);
                    this.Log("Include the .exe you tried to unpack.", LogMessageType.Error);
                    this.Log("", LogMessageType.Error);
                    return false;
            }

            return true;
        }

        protected override bool Unpack(V20Run run, SteamlessOptions options)
        {
            this.Step(run, "Decrypt the main code section.");
            if (!this.TryDecodeCodeSection(run))
                return false;

            this.Step(run, "Remove the .bind section.");
            return run.Bind.Remove(run.File, options, this.LoggingService);
        }

        protected override uint GetEntryPoint(V20Run run) => (uint)run.File.RvaFromVa(run.Header.OEP);

        private bool TryDecodeCodeSection(V20Run run)
        {
            var codeSectionRva = (ulong)run.File.BaseOfCode;
            var codeSection = run.File.FindSectionAtRva(codeSectionRva);

            // Some variants report a wrong BaseOfCode. Fall back to the stub header's code section
            // address only when the default does not map to a valid section, so working variants
            // are unaffected.
            if (codeSection == null || !codeSection.IsValid)
            {
                if (run.Header.CodeSectionVirtualAddress != 0)
                {
                    var stubSectionRva = run.File.RvaFromVa(run.Header.CodeSectionVirtualAddress);
                    var stubSection = run.File.FindSectionAtRva(stubSectionRva);
                    if (stubSection != null && stubSection.IsValid)
                        codeSectionRva = stubSectionRva;
                }
            }

            codeSection = run.File.FindSectionAtRva(codeSectionRva);
            if (codeSection == null || !codeSection.IsValid)
                return false;

            if (!StubDrmFlags.EncryptsCodeSectionV20(run.Header.Flags))
                return true;

            var codeSectionData = run.File.ReadSection(codeSection);

            // Rolling XOR: the declared size can exceed the real section, so cap the walk.
            var key = run.Header.CodeSectionXorKey;
            var blockCount = Math.Min(run.Header.CodeSectionSize >> 2, codeSection.SizeOfRawData >> 2);

            StubCrypto.Xor(ref codeSectionData, blockCount * 4, key);

            run.File.ReplaceSection(codeSection, codeSectionData);
            return true;
        }
    }

    /// <summary>
    /// Run state for a 2.0 unpack. The header size decides which of the three structs it is, so the
    /// interface holds them.
    /// </summary>
    public sealed class V20Run : UnpackRun
    {
        public ISteamStub32Var20Header Header { get; set; }
    }
}
