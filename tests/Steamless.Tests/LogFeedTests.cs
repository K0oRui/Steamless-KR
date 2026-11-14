namespace Steamless.Tests
{
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.Services;
    using System.Collections.Generic;

    /// <summary>
    /// The feed both hosts render: which messages survive, what a line looks like, and how a long
    /// session is bounded. No host is involved, so the rules are checked where they are written.
    /// </summary>
    internal static class LogFeedTests
    {
        private sealed class Sender
        {
        }

        public static void DebugLinesAreHiddenUntilVerbose()
        {
            var verbose = false;
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => verbose);

            service.OnAddLogMessage(null, new LogMessageEventArgs("hidden", LogMessageType.Debug));
            service.OnAddLogMessage(null, new LogMessageEventArgs("kept", LogMessageType.Information));

            Assert.Equal(1, feed.Entries.Count, "entries while quiet");
            Assert.Equal("kept", feed.Entries[0].Message, "message that survives");

            verbose = true;
            service.OnAddLogMessage(null, new LogMessageEventArgs("shown", LogMessageType.Debug));

            Assert.Equal(2, feed.Entries.Count, "entries while verbose");
            Assert.Equal("shown", feed.Entries[1].Message, "debug message after the switch");
        }

        public static void ALineNamesItsSource()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);

            service.OnAddLogMessage(null, new LogMessageEventArgs("host line", LogMessageType.Information));
            service.OnAddLogMessage(new Sender(), new LogMessageEventArgs("plugin line", LogMessageType.Information));
            feed.Add("host message", LogMessageType.Warning);

            Assert.Equal("[Steamless] host line", feed.Entries[0].Line, "line from the host");
            Assert.Equal("[Steamless.Tests] plugin line", feed.Entries[1].Line, "line from a plugin");
            Assert.Equal("[Steamless] host message", feed.Entries[2].Line, "line added by the host");
        }

        public static void AnEmptyMessageKeepsAnEmptyLine()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);

            service.OnAddLogMessage(null, new LogMessageEventArgs(string.Empty, LogMessageType.Error));

            Assert.Equal(string.Empty, feed.Entries[0].Line, "empty line");
        }

        public static void ACapacityDropsTheOldestFifth()
        {
            var service = new LoggingService();
            var changes = new List<LogChange>();

            using var feed = new LogFeed(service, () => true, 10);
            feed.Changed += (sender, change) => changes.Add(change);

            for (var x = 1; x <= 11; x++)
                feed.Add(x.ToString(), LogMessageType.Information);

            Assert.Equal(9, feed.Entries.Count, "entries after the trim");
            Assert.Equal("3", feed.Entries[0].Message, "oldest entry after the trim");

            Assert.Equal(12, changes.Count, "changes announced");
            Assert.Equal(LogChangeKind.Removed, changes[11].Kind, "the trim is announced last");
            Assert.Equal(2, changes[11].Entries.Count, "entries the trim removed");
            Assert.Equal("1", changes[11].Entries[0].Message, "first entry the trim removed");
            Assert.Equal("2", changes[11].Entries[1].Message, "last entry the trim removed");
        }

        public static void AnUnboundedFeedKeepsEveryEntry()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);

            for (var x = 0; x < 50; x++)
                feed.Add(x.ToString(), LogMessageType.Information);

            Assert.Equal(50, feed.Entries.Count, "entries without a capacity");
        }

        public static void ClearEmptiesTheFeed()
        {
            var service = new LoggingService();
            var changes = new List<LogChange>();

            using var feed = new LogFeed(service, () => true);
            feed.Changed += (sender, change) => changes.Add(change);

            feed.Add("first", LogMessageType.Information);
            feed.Add("second", LogMessageType.Information);
            feed.Clear();

            Assert.Equal(0, feed.Entries.Count, "entries after clear");
            Assert.Equal(LogChangeKind.Cleared, changes[changes.Count - 1].Kind, "the clear change");
            Assert.Equal(0, changes[changes.Count - 1].Entries.Count, "a clear carries no entries");
        }

        public static void DisposeStopsTheFeed()
        {
            var service = new LoggingService();
            var changes = new List<LogChange>();
            var feed = new LogFeed(service, () => true);

            feed.Changed += (sender, change) => changes.Add(change);
            feed.Dispose();
            service.OnAddLogMessage(null, new LogMessageEventArgs("late", LogMessageType.Information));

            Assert.Equal(0, feed.Entries.Count, "entries after dispose");
            Assert.Equal(0, changes.Count, "changes after dispose");
        }
    }
}
