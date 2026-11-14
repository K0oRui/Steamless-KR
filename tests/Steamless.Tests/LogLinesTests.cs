namespace Steamless.Tests
{
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.Services;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The host-side list: it replays the feed's changes exactly, including the entries a trim
    /// dropped, and it does the replay on the thread the host hands it.
    /// </summary>
    internal static class LogLinesTests
    {
        public static void AListFollowsTheFeed()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var lines = new LogLines(feed);

            feed.Add("first", LogMessageType.Information);
            feed.Add("second", LogMessageType.Warning);

            Assert.Equal(2, lines.Items.Count, "lines");
            Assert.Equal("first", lines.Items[0].Message, "first line");
            Assert.Equal("second", lines.Items[1].Message, "second line");
        }

        public static void AListDropsTheEntriesTheFeedDropped()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true, 10);
            using var lines = new LogLines(feed);

            for (var x = 1; x <= 11; x++)
                feed.Add(x.ToString(), LogMessageType.Information);

            Assert.Equal(feed.Entries.Count, lines.Items.Count, "line count matches the feed");
            Assert.Equal("3", lines.Items[0].Message, "oldest line after the trim");

            for (var x = 0; x < feed.Entries.Count; x++)
                Assert.Equal(feed.Entries[x].Message, lines.Items[x].Message, $"line {x} matches the feed");
        }

        public static void AListClearsWithTheFeed()
        {
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var lines = new LogLines(feed);

            feed.Add("first", LogMessageType.Information);
            feed.Clear();

            Assert.Equal(0, lines.Items.Count, "lines after the clear");
        }

        public static void AListWaitsForTheHostThread()
        {
            var service = new LoggingService();
            var queued = new Queue<Action>();

            using var feed = new LogFeed(service, () => true);
            using var lines = new LogLines(feed, queued.Enqueue);

            feed.Add("one", LogMessageType.Information);

            Assert.Equal(0, lines.Items.Count, "lines before the host runs a change");

            while (queued.Count > 0)
                queued.Dequeue()();

            Assert.Equal(1, lines.Items.Count, "lines after the host runs the change");
            Assert.Equal("one", lines.Items[0].Message, "line after the host runs the change");
        }

        public static void AListBatchesQueuedChanges()
        {
            var service = new LoggingService();
            var queued = new Queue<Action>();

            using var feed = new LogFeed(service, () => true);
            using var lines = new LogLines(feed, queued.Enqueue);

            feed.Add("one", LogMessageType.Information);
            feed.Add("two", LogMessageType.Information);
            feed.Add("three", LogMessageType.Information);

            Assert.Equal(1, queued.Count, "one drain for the burst");

            while (queued.Count > 0)
                queued.Dequeue()();

            Assert.Equal(3, lines.Items.Count, "lines after the drain");
        }

        public static void DisposeDropsAQueuedChange()
        {
            var service = new LoggingService();
            var queued = new Queue<Action>();

            using var feed = new LogFeed(service, () => true);
            var lines = new LogLines(feed, queued.Enqueue);

            feed.Add("before", LogMessageType.Information);
            lines.Dispose();

            while (queued.Count > 0)
                queued.Dequeue()();

            Assert.Equal(0, lines.Items.Count, "a change queued before dispose");
        }
    }
}
