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
    using API.Model;
    using API.PE;
    using System;

    /// <summary>
    /// The shared flow every SteamStub unpacker follows. A plugin supplies what is its own: whether
    /// a file is its variant, how to read its header, and the steps between the header and the
    /// write. The ordering, the `.bind` step, the packed-with line, the prologue claim, the header
    /// magic, the bounds-checked read, the write and the checksum are not a plugin's business.
    /// </summary>
    /// <typeparam name="TRun">
    /// The run state this variant carries. A plugin adds its header and intermediate data there.
    /// </typeparam>
    public abstract class StubUnpacker<TRun> : SteamlessPlugin where TRun : UnpackRun, new()
    {
        /// <summary>The name used in the packed-with line, e.g. "SteamStub Variant 3.1".</summary>
        protected abstract string VariantName { get; }

        public override string Author => "atom0s";

        /// <summary>
        /// False when the file's bitness rules this variant out. Stubs before 3.x shipped only as
        /// 32-bit, so that is the default; the 3.x plugin handles both widths.
        /// </summary>
        protected virtual bool AcceptsBitness(PeImage file) => !file.Is64Bit;

        /// <summary>
        /// The stub prologue that identifies this variant in `.bind`, or null for a variant that
        /// claims files another way. The stubs before 3.x differ in exactly this pattern.
        /// </summary>
        protected virtual string BindPrologue => null;

        /// <summary>
        /// Whether this variant claims the image. Called after the bitness check and the `.bind`
        /// open, so a claim rule only has to recognise its own signature. The default looks for
        /// <see cref="BindPrologue"/>; 3.x reads its own header size instead.
        /// </summary>
        protected virtual bool CanUnpack(PeImage file, BindSection bind)
        {
            var prologue = this.BindPrologue;
            return prologue != null && PeHelpers.FindPattern(bind.Data, prologue) != -1;
        }

        protected abstract bool ReadHeader(TRun run);

        /// <summary>Everything between the header and the write, in the variant's order.</summary>
        protected abstract bool Unpack(TRun run, SteamlessOptions options);

        /// <summary>The original entry point to write, computed from whatever the run holds.</summary>
        protected abstract uint GetEntryPoint(TRun run);

        public override bool CanProcessFile(PeImage file)
        {
            if (!this.AcceptsBitness(file))
                return false;

            var bind = BindSection.Open(file);
            return bind != null && this.CanUnpack(file, bind);
        }

        public override bool ProcessFile(PeImage file, SteamlessOptions options)
        {
            var run = new TRun { File = file };

            run.Bind = BindSection.Open(run.File);
            if (run.Bind == null)
                return false;

            this.Log($"File is packed with {this.VariantName}.", LogMessageType.Information);

            if (!this.ReadHeader(run))
                return false;

            if (!this.Unpack(run, options))
                return false;

            this.Step(run, "Write the unpacked file.");
            if (options.RecalculateFileChecksum)
                this.Step(run, "Update the file checksum.");

            // The save writes the file and, when asked, folds the checksum in before the temp file
            // replaces the original, so both steps above are one call.
            if (!UnpackedOutput.Write(run.File, run.Bind, this.GetEntryPoint(run), options, this.LoggingService))
                return false;

            return true;
        }

        /// <summary>
        /// Logs the next numbered step. The counter lives on the run, so the template can add the
        /// write and checksum steps after a variant's own without the variant knowing the total.
        /// </summary>
        protected void Step(UnpackRun run, string message)
        {
            this.Log($"Step {++run.Step} - {message}", LogMessageType.Information);
        }

        /// <summary>
        /// The magic a 2.x stub writes four bytes before its entry point. A file without it is not
        /// that variant, and the check is cheaper than the header search it avoids.
        /// </summary>
        protected static bool HasHeaderMagic(PeImage file, uint magic)
        {
            var offset = file.FileOffsetFromRva(file.AddressOfEntryPoint);
            return offset >= 4 &&
                   file.TryRead(offset - 4, 4, out var bytes) &&
                   BitConverter.ToUInt32(bytes.Span) == magic;
        }

        /// <summary>
        /// Reads a header whose address and size the stub declares. The size is attacker controlled,
        /// so a zero or a range outside the file is rejected before the copy.
        /// </summary>
        protected static bool TryReadHeaderWindow(PeImage file, ulong rva, uint size, out byte[] data)
        {
            data = Array.Empty<byte>();

            if (size == 0)
                return false;

            var offset = file.FileOffsetFromRva(rva);
            if (!file.TryRead(offset, size, out var window))
                return false;

            data = window.ToArray();
            return true;
        }
    }
}
