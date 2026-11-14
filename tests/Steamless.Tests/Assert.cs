namespace Steamless.Tests
{
    using System;

    /// <summary>
    /// The few checks the tests need. A failure throws, and the runner reports the message, so the
    /// project needs no test framework and no package reference.
    /// </summary>
    internal static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        public static void False(bool condition, string message)
        {
            if (condition)
                throw new Exception(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new Exception($"{message}: expected {expected}, got {actual}");
        }

        public static void SameBytes(byte[] expected, byte[] actual, string message)
        {
            if (expected.Length != actual.Length)
                throw new Exception($"{message}: expected {expected.Length} bytes, got {actual.Length}");

            for (var x = 0; x < expected.Length; x++)
            {
                if (expected[x] != actual[x])
                    throw new Exception($"{message}: byte {x} is 0x{actual[x]:X2}, expected 0x{expected[x]:X2}");
            }
        }

        public static void BytesInRange(byte value, byte[] data, int offset, int count, string message)
        {
            for (var x = offset; x < offset + count; x++)
            {
                if (data[x] != value)
                    throw new Exception($"{message}: byte {x} is 0x{data[x]:X2}, expected 0x{value:X2}");
            }
        }

        public static void Throws<T>(Action body, string message) where T : Exception
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                if (ex is T)
                    return;

                throw new Exception($"{message}: expected {typeof(T).Name}, got {ex.GetType().Name}");
            }

            throw new Exception($"{message}: expected {typeof(T).Name}, nothing was thrown");
        }
    }
}
