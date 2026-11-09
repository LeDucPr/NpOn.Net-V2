using System.Buffers;

namespace Common.Extensions.NpOn.ICommonDb.DbResults.Blocks;

public interface INpOnBlock : IDisposable
{
    int RowCount { get; }
    Type ClrType { get; }
    bool IsNull(int rowIndex);
    object? GetObject(int rowIndex);
}

public sealed class Int64Block : INpOnBlock
{
    public long[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(long);

    public Int64Block(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<long>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<long>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class Int32Block : INpOnBlock
{
    public int[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(int);

    public Int32Block(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<int>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<int>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class DoubleBlock : INpOnBlock
{
    public double[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(double);

    public DoubleBlock(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<double>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<double>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class GuidBlock : INpOnBlock
{
    public Guid[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(Guid);

    public GuidBlock(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<Guid>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<Guid>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class DateTimeBlock : INpOnBlock
{
    public DateTime[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(DateTime);

    public DateTimeBlock(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<DateTime>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<DateTime>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class BooleanBlock : INpOnBlock
{
    public bool[] Buffer;
    public bool[]? NullMask;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(bool);

    public BooleanBlock(int rowCount, bool hasNulls = false)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<bool>.Shared.Rent(rowCount);
        if (hasNulls)
            NullMask = ArrayPool<bool>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => NullMask != null && NullMask[rowIndex];
    public object? GetObject(int rowIndex) => IsNull(rowIndex) ? null : Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<bool>.Shared.Return(Buffer);
        if (NullMask != null)
            ArrayPool<bool>.Shared.Return(NullMask);
    }
}

public sealed class StringBlock : INpOnBlock
{
    public string?[] Buffer;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(string);

    public StringBlock(int rowCount)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<string?>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => Buffer[rowIndex] == null;
    public object? GetObject(int rowIndex) => Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<string?>.Shared.Return(Buffer, clearArray: true);
    }
}

public sealed class ObjectBlock : INpOnBlock
{
    public object?[] Buffer;
    private readonly int _rowCount;
    private bool _disposed;

    public int RowCount => _rowCount;
    public Type ClrType => typeof(object);

    public ObjectBlock(int rowCount)
    {
        _rowCount = rowCount;
        Buffer = ArrayPool<object?>.Shared.Rent(rowCount);
    }

    public bool IsNull(int rowIndex) => Buffer[rowIndex] == null || Buffer[rowIndex] is DBNull;
    public object? GetObject(int rowIndex) => Buffer[rowIndex];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<object?>.Shared.Return(Buffer, clearArray: true);
    }
}

