namespace Steamless.Tests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Runs every check and returns a non-zero exit code when one fails, so CI needs no test runner
    /// and the project needs no package reference.
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            var checks = new (string Name, Action Body)[]
            {
                ("PE reader loads PE32 and PE32+", PeImageTests.LoadsBothFlavours),
                ("PE reader rejects broken layouts", PeImageTests.RejectsBrokenFiles),
                ("PE reader accepts a bodyless section at offset zero", PeImageTests.AcceptsBodylessSectionAtOffsetZero),
                ("PE writer keeps a bodyless last section out of the overlay", PeImageTests.SaveTreatsABodylessLastSectionAsTheEndOfTheSections),
                ("PE writer repairs the import and certificate entries", PeImageTests.RepairsHeaderPointersAfterSectionRemoval),
                ("PE writer reproduces the file it read", PeImageTests.SaveReproducesTheFile),
                ("PE writer replaces one section body", PeImageTests.SaveReplacesOneSection),
                ("PE writer computes and clears the checksum", PeImageTests.SaveComputesAndClearsTheChecksum),
                ("PE writer checksums a file larger than one read", PeImageTests.SaveChecksumsAFileLargerThanOneRead),
                ("PE writer drops a removed section", PeImageTests.SaveDropsARemovedSection),
                ("PE writer pads a realigned section that ends the file", PeImageTests.SaveHandlesARealignedSectionAtTheEndOfTheFile),
                ("PE section edits need an explicit replace", PeImageTests.AChangedBodyReachesTheFileOnlyAfterReplace),
                ("PE writer rejects a foreign or oversized body", PeImageTests.ReplaceSectionRejectsAForeignOrOversizedBody),
                ("Pattern scan handles wildcards", PeHelpersTests.FindPatternHandlesWildcards),
                ("The default claim looks for the bind prologue", StubUnpackerTests.TheDefaultClaimLooksForThePrologue),
                ("The default claim rejects 64-bit files", StubUnpackerTests.TheDefaultClaimRejects64BitFiles),
                ("A header window outside the file is rejected", StubUnpackerTests.AHeaderWindowOutsideTheFileIsRejected),
                ("The header magic sits before the entry point", StubUnpackerTests.TheHeaderMagicSitsBeforeTheEntryPoint),
                ("A 2.0 header sets the code-section bit when encrypted", StubDrmFlagsTests.A20HeaderSetsTheBitWhenEncrypted),
                ("A 2.1 header clears the code-section bit when encrypted", StubDrmFlagsTests.A21HeaderClearsTheBitWhenEncrypted),
                ("Range check rejects bad ranges", PeHelpersTests.IsValidRangeRejectsNegativeAndOverflowingRanges),
                ("Rolling XOR decrypts with a given key", StubCryptoTests.XorDecryptsWithAGivenKey),
                ("Rolling XOR uses the first word as its key", StubCryptoTests.XorUsesTheFirstWordWhenNoKeyIsGiven),
                ("Code section AES restores the stolen block", AesHelperTests.DecryptRestoresTheStolenBlockAndChunkedInput),
                ("Code section AES rejects a partial block", AesHelperTests.DecryptRejectsAMisalignedSource),
                ("Code section decrypt honours the stub size rule", CodeSectionDecryptorTests.DecryptsTheSectionForBothSizeRules),
                ("Folder input finds upper-case executables", UnpackSessionTests.FolderInputFindsUpperCaseExecutables),
                ("Run report counts every outcome", UnpackSessionTests.ReportCountsEveryOutcome),
                ("Session runs the plugin that claims", UnpackSessionPluginTests.AutomaticModeRunsThePluginThatClaims),
                ("Session skips a plugin that does not claim", UnpackSessionPluginTests.AutomaticModeSkipsAPluginThatDoesNotClaim),
                ("Session reports a claim that fails", UnpackSessionPluginTests.AutomaticModeReportsAClaimThatFails),
                ("Session hands the next plugin a clean file", UnpackSessionPluginTests.AFailedClaimLeavesTheNextPluginACleanFile),
                ("Session contains a claim that throws", UnpackSessionPluginTests.AutomaticModeContainsAPluginThatThrowsOnClaim),
                ("An errored claim lets the next plugin try", UnpackSessionPluginTests.AnErroredClaimLetsTheNextPluginTry),
                ("Session contains an unpack that throws", UnpackSessionPluginTests.AutomaticModeContainsAPluginThatThrowsOnProcess),
                ("Session runs only the chosen plugin", UnpackSessionPluginTests.ExplicitSelectionRunsOnlyTheChosenPlugin),
                ("Picker lists every variant of a plugin", UnpackSessionPluginTests.PickerListsEveryVariantOfAPlugin),
                ("An explicit variant rejects another layout", UnpackSessionPluginTests.ExplicitVariantRejectsAnotherVariant),
                ("An explicit variant runs its own layout", UnpackSessionPluginTests.ExplicitVariantRunsTheMatchingFile),
                ("Session stops on cancellation", UnpackSessionPluginTests.CancellationStopsTheRun),
                ("Session disposes its plugins", UnpackSessionPluginTests.DisposeDisposesEveryPlugin),
                ("Session rejects a run after dispose", UnpackSessionPluginTests.RunAfterDisposeThrows),
                ("Detection lists the claiming plugins", UnpackSessionPluginTests.DetectListsTheClaimingPlugins),
                ("Detection uses the plugin variant name", UnpackSessionPluginTests.DetectionUsesThePluginsVariantName),
                ("Run returns the report", SessionRunTests.ARunReturnsItsReport),
                ("Cancelling a run stops it", SessionRunTests.CancellingARunStopsIt),
                ("Run starts only once", SessionRunTests.ARunStartsOnce),
                ("A run returns its report", UnpackRunnerTests.ARunReturnsItsReport),
                ("A multi-file run ends with one total", UnpackRunnerTests.AMultiFileRunEndsWithOneTotal),
                ("A cancelled run is logged and released", UnpackRunnerTests.ACancelledRunIsLogged),
                ("A run that throws is contained", UnpackRunnerTests.ARunThatThrowsIsContained),
                ("A second run is refused while one is in flight", UnpackRunnerTests.ASecondRunIsRefusedWhileOneIsInFlight),
                ("Log feed hides debug lines until verbose", LogFeedTests.DebugLinesAreHiddenUntilVerbose),
                ("Log line names its source", LogFeedTests.ALineNamesItsSource),
                ("Log feed keeps an empty line empty", LogFeedTests.AnEmptyMessageKeepsAnEmptyLine),
                ("Log feed drops its oldest fifth", LogFeedTests.ACapacityDropsTheOldestFifth),
                ("Log feed without a capacity keeps everything", LogFeedTests.AnUnboundedFeedKeepsEveryEntry),
                ("Clearing the log feed empties it", LogFeedTests.ClearEmptiesTheFeed),
                ("Disposing the log feed stops it", LogFeedTests.DisposeStopsTheFeed),
                ("Log list follows the feed", LogLinesTests.AListFollowsTheFeed),
                ("Log list batches a burst", LogLinesTests.AListBatchesQueuedChanges),
                ("Log list drops what the feed dropped", LogLinesTests.AListDropsTheEntriesTheFeedDropped),
                ("Log list clears with the feed", LogLinesTests.AListClearsWithTheFeed),
                ("Log list waits for the host thread", LogLinesTests.AListWaitsForTheHostThread),
                ("Log list drops a queued change after dispose", LogLinesTests.DisposeDropsAQueuedChange),
            };

            var failures = new List<string>();

            foreach (var check in checks)
            {
                try
                {
                    check.Body();
                    Console.WriteLine($"  pass  {check.Name}");
                }
                catch (Exception ex)
                {
                    failures.Add(check.Name);
                    Console.WriteLine($"  FAIL  {check.Name}");
                    Console.WriteLine($"        {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine(failures.Count == 0
                ? $"{checks.Length} checks passed."
                : $"{failures.Count} of {checks.Length} checks failed.");

            return failures.Count == 0 ? 0 : 1;
        }
    }
}
