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

namespace Steamless.Unpacker.Variant10.x86
{
    using API;
    using API.Model;
    using API.PE;
    using Classes;
    using Common;
    using System;

    [SteamlessApiVersion(1, 2)]
    public class Main : StubUnpacker<UnpackRun>
    {
        private const string StubPrologue = "60 81 EC 00 10 00 00 BE ?? ?? ?? ?? B9 6A";

        private const string EntryPointPrologue = "61 B8 ?? ?? ?? ?? FF E0";

        protected override string VariantName => "SteamStub Variant 1.0";

        public override string Name => "SteamStub Variant 1.0 Unpacker (x86)";

        public override string Description => "Unpacks 32-bit SteamStub variant 1.0.";

        protected override string BindPrologue => StubPrologue;

        protected override bool ReadHeader(UnpackRun run)
        {
            this.Step(run, "Read and validate the SteamStub header.");

            var offset = PeHelpers.FindPattern(run.Bind.Data, StubPrologue);
            if (offset == -1)
                return false;

            var headerAddress = BitConverter.ToUInt32(run.Bind.Data, (int)offset + 8);
            var headerWords = BitConverter.ToUInt32(run.Bind.Data, (int)offset + 13);
            if (headerWords > 0x3FFFFFFF)
                return false;

            var headerSize = headerWords * 4;

            if (!TryReadHeaderWindow(run.File, headerAddress - (uint)run.File.ImageBase, headerSize, out var headerData))
                return false;

            // The v1.0 header is obfuscated by XOR-ing each byte with the square of its index.
            for (var x = 0; x < headerData.Length; x++)
                headerData[x] ^= (byte)(x * x);

            var header = PeHelpers.GetStructure<SteamStub32Var10Header>(headerData);

            // The unpacker function must resolve to the file's entry point.
            if (header.BindFunction - (uint)run.File.ImageBase != run.File.AddressOfEntryPoint)
                return false;

            offset = PeHelpers.FindPattern(run.Bind.Data, EntryPointPrologue);
            if (offset == -1)
                return false;

            run.EntryPoint = BitConverter.ToUInt32(run.Bind.Data, (int)offset + 2) - (uint)run.File.ImageBase;

            return true;
        }

        protected override bool Unpack(UnpackRun run, SteamlessOptions options)
        {
            this.Step(run, "Remove the .bind section.");
            return run.Bind.Remove(run.File, options, this.LoggingService);
        }

        protected override uint GetEntryPoint(UnpackRun run) => run.EntryPoint;
    }
}
