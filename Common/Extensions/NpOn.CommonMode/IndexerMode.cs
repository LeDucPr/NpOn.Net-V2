using System.Buffers.Text;
using System.Runtime.InteropServices;

namespace Common.Extensions.NpOn.CommonMode;

/// <summary>
/// Centralized GUID and identifier generation utility for NpOn.Net-V2.
/// Provides specialized Guid types tuned for database indexing, distributed Sagas,
/// high-throughput audit logging, and security tokens.
/// </summary>
public static class IndexerMode
{
    /// <summary>
    /// Default GUID generator: Returns time-ordered UUIDv7 (RFC 9562).
    /// Monotonically increasing, optimal for database primary keys and B-Tree indexing.
    /// </summary>
    public static Guid CreateGuid()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Generates time-ordered UUID (Version 7 - RFC 9562) with current UTC timestamp.
    /// Eliminates B-Tree index page fragmentation and maximizes insertion throughput.
    /// Best for: PostgreSQL, ScyllaDB, MSSQL Primary Keys, Saga instances, Outbox messages.
    /// </summary>
    public static Guid CreateGuidV7()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Generates time-ordered UUID (Version 7) pinned to a specific timestamp.
    /// Optimal for: Historical data backfilling, event replay, and deterministic event ordering.
    /// </summary>
    public static Guid CreateGuidV7(DateTimeOffset timestamp)
    {
        return Guid.CreateVersion7(timestamp);
    }

    /// <summary>
    /// Generates cryptographically random UUID (Version 4).
    /// Best for: Session tokens, nonces, correlation IDs, or scenarios where timestamp must remain unexposed.
    /// </summary>
    public static Guid CreateGuidV4()
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// Dedicated identifier for Distributed Saga workflows (Version 7).
    /// Provides chronological tracking of Saga instances across microservices.
    /// </summary>
    public static Guid CreateSagaId()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Dedicated identifier for Audit Log entries (Version 7).
    /// Guarantees that consecutive audit entries sort strictly in time order.
    /// </summary>
    public static Guid CreateAuditId()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Generates MSSQL-optimized COMB GUID (Component-based Guid).
    /// In MSSQL uniqueidentifier clustered index, the last 6 bytes have the highest precedence.
    /// </summary>
    public static Guid CreateCombGuid()
    {
        byte[] guidArray = Guid.NewGuid().ToByteArray();
        DateTime now = DateTime.UtcNow;
        long days = (now.Ticks - new DateTime(1900, 1, 1).Ticks) / TimeSpan.TicksPerDay;
        TimeSpan msecs = now.TimeOfDay;

        byte[] daysArray = BitConverter.GetBytes(days);
        byte[] msecsArray = BitConverter.GetBytes((long)(msecs.TotalMilliseconds / 3.333333));

        // MSSQL sorts by the last 6 bytes (bytes 10 through 15)
        Array.Copy(daysArray, 0, guidArray, 10, 2);
        Array.Copy(msecsArray, 0, guidArray, 12, 4);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(daysArray);
            Array.Reverse(msecsArray);
        }

        return new Guid(guidArray);
    }

    /// <summary>
    /// Generates URL-safe, compact 22-character string from a Guid (Base64 URL format).
    /// Best for: HTTP route parameters, public entity IDs, compact tokens.
    /// </summary>
    public static string CreateShortGuid(Guid? guid = null)
    {
        var targetGuid = guid ?? Guid.CreateVersion7();
        Span<byte> guidBytes = stackalloc byte[16];
        MemoryMarshal.TryWrite(guidBytes, in targetGuid);

        Span<byte> base64Bytes = stackalloc byte[24];
        Base64.EncodeToUtf8(guidBytes, base64Bytes, out _, out _);

        // Convert Base64 to URL-safe by replacing '+' with '-' and '/' with '_'
        Span<char> chars = stackalloc char[22];
        for (int i = 0; i < 22; i++)
        {
            byte b = base64Bytes[i];
            chars[i] = b switch
            {
                (byte)'+' => '-',
                (byte)'/' => '_',
                _ => (char)b
            };
        }

        return new string(chars);
    }

    /// <summary>
    /// Returns formatted Guid string (Default: Version 7).
    /// </summary>
    public static string CreateGuidAsString()
    {
        return Guid.CreateVersion7().ToString();
    }

    /// <summary>
    /// Returns formatted Guid string in format "N" (32 hex characters without hyphens).
    /// </summary>
    public static string CreateHexGuidString()
    {
        return Guid.CreateVersion7().ToString("N");
    }

    /// <summary>
    /// Diagnostic helper capturing caller method and callstack metadata with Guid.
    /// </summary>
    public static string CreateGuidWithStackTrace()
    {
        var st = new System.Diagnostics.StackTrace();
        var sf = st.GetFrame(1);
        var method = sf?.GetMethod();
        return $"{method?.DeclaringType?.FullName}.{method?.Name}_{Guid.CreateVersion7()}";
    }
}
