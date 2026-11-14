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
    /// The steps the 3.0 and 3.1 stubs share. Their headers describe the same layout at different
    /// widths, so one search finds the header and one sequence consumes it. A variant supplies its
    /// accepted header sizes and its struct decoding.
    /// </summary>
    public abstract class Stub3xUnpacker : StubUnpacker<Stub3xRun>
    {
        /// <summary>
        /// The declared header size when this variant accepts it, otherwise zero. The stub writes
        /// its own header length in its prologue, and that value separates the variants.
        /// </summary>
        protected abstract uint ResolveHeaderSize(PeImage file, BindSection bind);

        /// <summary>
        /// Decodes a declared-size header buffer into the run's normalised header. Returns false
        /// when the signature does not match, which means the buffer was not the header.
        /// </summary>
        /// <remarks>
        /// The buffer is scratch for this attempt and is refilled before the next one, so a variant
        /// may decrypt it in place. A variant whose struct is wider than the declared size has to
        /// copy into its own buffer first.
        /// </remarks>
        protected abstract bool TryDecodeHeader(Stub3xRun run, byte[] data);

        /// <summary>SteamStub 3.x shipped in both widths, so neither rules this plugin out.</summary>
        protected override bool AcceptsBitness(PeImage file) => true;

        protected override bool CanUnpack(PeImage file, BindSection bind)
        {
            return this.ResolveHeaderSize(file, bind) != 0;
        }

        /// <summary>
        /// Looks for the header in the three places a 3.x stub keeps it: before the entry point,
        /// before a TLS callback, and anywhere in `.bind`. The order matters, because the first two
        /// are exact and the third is a scan.
        /// </summary>
        protected override bool ReadHeader(Stub3xRun run)
        {
            this.Step(run, "Read and validate the SteamStub header.");

            var headerSize = this.ResolveHeaderSize(run.File, run.Bind);
            if (headerSize == 0)
                return false;

            // Attempt 1: the header sits immediately before the entry point.
            var offset = run.File.FileOffsetFromRva(run.File.AddressOfEntryPoint);
            if (offset >= headerSize &&
                run.File.TryRead(offset - headerSize, headerSize, out var window))
            {
                if (this.TryDecodeHeader(run, window.ToArray()))
                {
                    run.StubStartRva = run.File.AddressOfEntryPoint;
                    return true;
                }
            }

            // Attempt 2: the stub starts at a TLS callback instead.
            if (run.File.TlsCallbacks.Count > 0)
            {
                offset = run.File.FileOffsetFromRva(run.File.RvaFromVa(run.File.TlsCallbacks[0]));
                if (offset >= headerSize &&
                    run.File.TryRead(offset - headerSize, headerSize, out window))
                {
                    if (this.TryDecodeHeader(run, window.ToArray()))
                    {
                        run.TlsEntryPoint = run.File.RvaFromVa(run.File.TlsCallbacks[0]);
                        run.StubStartRva = run.TlsEntryPoint;

                        // The repair only applies when the header asks for it. The 3.1 header has
                        // no such flag and projects one, so its callbacks are always repaired. If
                        // the repair fails, for example when the XOR pattern is missing, the write
                        // step falls back to the header entry point.
                        if (run.Header.HasTlsCallback == 1)
                            run.TlsEntryPointOverride = TlsCallbackRepair.Repair(run.File, run.Header.OriginalEntryPoint, run.Header.XorKey);

                        return true;
                    }
                }
            }

            // Attempt 3: some files have an entry point outside `.bind`, so scan the section. The
            // stub start has to come from where the header was found; the entry point says nothing
            // about it here.
            var bind = run.Bind.Data;
            var data = new byte[headerSize];
            for (var scan = 0; scan + headerSize <= bind.Length; scan += 4)
            {
                Array.Copy(bind, scan, data, 0, headerSize);
                if (this.TryDecodeHeader(run, data))
                {
                    run.StubStartRva = run.Bind.VirtualAddress + (ulong)scan + headerSize;
                    return true;
                }
            }

            return false;
        }

        protected override bool Unpack(Stub3xRun run, SteamlessOptions options)
        {
            // The header search records where the stub starts. The entry point only agrees with it
            // when the stub sits in front of the entry, which is not true of every file.
            var stubStart = run.StubStartRva;
            if (stubStart == 0)
                stubStart = run.TlsEntryPoint > 0 ? run.TlsEntryPoint : run.File.AddressOfEntryPoint;

            var payloadRva = stubStart - run.Header.BindSectionOffset;

            this.Step(run, "Decrypt the payload.");
            if (!Stub3xFlow.TryDecryptPayload(run.File, payloadRva, run.Header.PayloadSize, run.XorKey, PayloadSizing.Block,
                                              options, this.LoggingService, out _))
                return false;

            this.Step(run, "Decrypt and dump SteamDRMP.dll.");
            if (run.Header.DRMPDllSize == 0)
            {
                this.Log(" --> No SteamDRMP.dll in this file.", LogMessageType.Debug);
            }
            else
            {
                this.Log(" --> SteamDRMP.dll found.", LogMessageType.Debug);

                var drmpOffset = run.File.FileOffsetFromRva(payloadRva + run.Header.DRMPDllOffset);
                if (!Stub3xFlow.TryDecryptDrmp(run.File, drmpOffset, run.Header.DRMPDllSize, run.Header.EncryptionKeys,
                                              options, this.LoggingService, out _))
                    return false;
            }

            this.Step(run, "Remove the .bind section and find the code section.");
            var encrypted = StubDrmFlags.EncryptsCodeSectionV21(run.Header.Flags);
            if (!Stub3xFlow.TryPrepareCodeSection(run.File, run.Bind, options, encrypted, run.Header.CodeSectionVirtualAddress,
                                                  this.LoggingService, out var codeSection))
                return false;

            this.Step(run, "Decrypt the code section.");
            return Stub3xFlow.TryDecryptCodeSection(run.File, codeSection, encrypted, run.Header.CodeSectionRawSize,
                                                    run.Header.AES_Key, run.Header.AES_IV, run.Header.CodeSectionStolenData,
                                                    this.LoggingService);
        }

        protected override uint GetEntryPoint(Stub3xRun run)
        {
            return run.TlsEntryPointOverride > 0
                ? run.TlsEntryPointOverride
                : (uint)run.Header.OriginalEntryPoint;
        }
    }
}
