#nullable enable

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

namespace Steamless.API.PE
{
    /// <summary>
    /// Why a file could not be read as a PE image. Each case names one check in
    /// <see cref="PeImage.Load"/>.
    /// </summary>
    public enum PeLoadFailure
    {
        None,

        FileMissing,

        Unreadable,

        TooSmall,

        NotDosExecutable,

        BadNtOffset,

        NotPortableExecutable,

        NoSections,

        OptionalHeaderOutsideFile,

        UnsupportedOptionalHeader,

        OptionalHeaderTruncated,

        BadAlignment,

        SectionTableOutsideFile,

        SectionOutsideFile,

        SectionOverlapsHeaders,
    }

    /// <summary>
    /// The outcome of reading a file: a parsed image, or the reason it was rejected. A host that
    /// gets a failure can say what is wrong with the file instead of guessing from a null.
    /// </summary>
    public sealed class PeLoadResult
    {
        private PeLoadResult(PeImage? image, PeLoadFailure failure)
        {
            this.Image = image;
            this.Failure = failure;
        }

        public PeImage? Image { get; }

        public PeLoadFailure Failure { get; }

        public bool Success => this.Image != null;

        /// <summary>A sentence fragment naming the reason, for a log line.</summary>
        public string Message => this.Failure switch
        {
            PeLoadFailure.None => "the file is a PE image",
            PeLoadFailure.FileMissing => "the file does not exist",
            PeLoadFailure.Unreadable => "the file could not be read",
            PeLoadFailure.TooSmall => "the file is too small to hold PE headers",
            PeLoadFailure.NotDosExecutable => "the file does not start with the MZ signature",
            PeLoadFailure.BadNtOffset => "the NT header offset points outside the file",
            PeLoadFailure.NotPortableExecutable => "the file does not carry the PE signature",
            PeLoadFailure.NoSections => "the file declares no sections",
            PeLoadFailure.OptionalHeaderOutsideFile => "the optional header runs past the end of the file",
            PeLoadFailure.UnsupportedOptionalHeader => "the optional header is neither PE32 nor PE32+",
            PeLoadFailure.OptionalHeaderTruncated => "the optional header is smaller than the format requires",
            PeLoadFailure.BadAlignment => "a section or file alignment is not a power of two",
            PeLoadFailure.SectionTableOutsideFile => "the section table runs past the end of the file",
            PeLoadFailure.SectionOutsideFile => "a section's raw data runs past the end of the file",
            PeLoadFailure.SectionOverlapsHeaders => "a section's raw data starts inside the file headers",
            _ => "the file is not a PE image",
        };

        public static PeLoadResult Loaded(PeImage image)
        {
            return new PeLoadResult(image, PeLoadFailure.None);
        }

        public static PeLoadResult Failed(PeLoadFailure failure)
        {
            return new PeLoadResult(null, failure);
        }
    }
}
