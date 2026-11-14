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
    /// <summary>
    /// The protection flags a SteamStub header records. Only the code-section bit changes what an
    /// unpacker does, and the two header generations read it in opposite senses: a 2.0 header sets
    /// it when the section is encrypted, while a 2.1 or 3.x header calls the same bit "no
    /// encryption" and clears it when encrypted. Both readings live here, so a caller names its
    /// generation.
    ///
    /// The rest of the word describes checks the stub performs: module verification, ownership,
    /// debugger and error dialog. None of them change what has to be removed.
    /// </summary>
    public static class StubDrmFlags
    {
        /// <summary>The bit both generations use for the code section's encryption.</summary>
        private const uint CodeSection = 0x04;

        /// <summary>
        /// Whether a 2.0 header says the stub encrypted the code section. That header sets the bit
        /// to mean encrypted.
        /// </summary>
        public static bool EncryptsCodeSectionV20(uint flags)
        {
            return (flags & StubDrmFlags.CodeSection) != 0;
        }

        /// <summary>
        /// Whether a 2.1 or 3.x header says the stub encrypted the code section. Those headers call
        /// the bit "no encryption", so a clear bit means encrypted.
        /// </summary>
        public static bool EncryptsCodeSectionV21(uint flags)
        {
            return (flags & StubDrmFlags.CodeSection) == 0;
        }
    }
}
