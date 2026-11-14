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

namespace Steamless.Unpacker.Variant3x
{
    using API;
    using API.PE;
    using Classes;
    using Common;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The SteamStub 3.x unpacker. A 3.0 and a 3.1 stub differ in their header signature, their
    /// declared header size and the width of four fields; everything after the header has the same
    /// layout. The declared size picks the layout, and the shared 3.x steps read one normalised
    /// header either way.
    /// </summary>
    [SteamlessApiVersion(1, 2)]
    public class Main : Stub3xUnpacker
    {
        private const uint HeaderSize30B0 = 0xB0;

        private const uint HeaderSize30D0 = 0xD0;

        private const uint HeaderSize31 = 0xF0;

        private const uint HeaderMagic30 = 0xC0DEC0DE;

        private const uint HeaderMagic31 = 0xC0DEC0DF;

        private const string VariantName30 = "SteamStub Variant 3.0 Unpacker (x86/x64)";

        private const string VariantName31 = "SteamStub Variant 3.1 Unpacker (x86/x64)";

        private static readonly string[] VariantNames = { VariantName30, VariantName31 };

        protected override string VariantName => "SteamStub Variant 3.x";

        public override string Name => "SteamStub Variant 3.x Unpacker";

        public override string Description => "Unpacks SteamStub variants 3.0 and 3.1, 32-bit and 64-bit.";

        /// <summary>The picker lists both layouts, and a pick pins the run to that layout.</summary>
        public override IReadOnlyList<string> Variants => VariantNames;

        /// <summary>
        /// Detection reports the layout this file uses, not the module's name, so the corpus sorter
        /// and the report still tell 3.0 from 3.1 even though one plugin claims both.
        /// </summary>
        public override string DetectVariant(PeImage file)
        {
            var bind = BindSection.Open(file);
            if (bind == null)
                return this.Name;

            var headerSize = this.ResolveHeaderSize(file, bind);

            if (headerSize == HeaderSize31)
                return VariantName31;
            if (headerSize == HeaderSize30B0 || headerSize == HeaderSize30D0)
                return VariantName30;

            return this.Name;
        }

        protected override uint ResolveHeaderSize(PeImage file, BindSection bind)
        {
            var headerSize = StubHeaderSize.Read(file, bind);
            return headerSize == HeaderSize30B0 || headerSize == HeaderSize30D0 || headerSize == HeaderSize31 ? headerSize : 0;
        }

        protected override bool TryDecodeHeader(Stub3xRun run, byte[] data)
        {
            switch ((uint)data.Length)
            {
                case HeaderSize30B0:
                case HeaderSize30D0:
                    return Main.TryDecodeVar30(run, data);
                case HeaderSize31:
                    return Main.TryDecodeVar31(run, data);
                default:
                    return false;
            }
        }

        private static bool TryDecodeVar30(Stub3xRun run, byte[] data)
        {
            // A 3.0 header can be 0xB0 or 0xD0 bytes while the struct is sized for 0xD0. Decode
            // the declared bytes into a full-size buffer so the tail reads as zero.
            var buffer = new byte[Marshal.SizeOf<SteamStubVar30Header>()];
            Array.Copy(data, buffer, data.Length);

            run.XorKey = StubCrypto.Xor(ref buffer, (uint)data.Length);
            var header = PeHelpers.GetStructure<SteamStubVar30Header>(buffer);

            if (header.Signature != HeaderMagic30)
                return false;

            run.Header = header.ToStub3xHeader();
            return true;
        }

        private static bool TryDecodeVar31(Stub3xRun run, byte[] data)
        {
            run.XorKey = StubCrypto.Xor(ref data, (uint)data.Length);
            var header = PeHelpers.GetStructure<SteamStubVar31Header>(data);

            if (header.Signature != HeaderMagic31)
                return false;

            run.Header = header.ToStub3xHeader();
            return true;
        }
    }
}
