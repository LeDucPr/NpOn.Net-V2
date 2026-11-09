using Common.Extensions.NpOn.ICommonDb.DbResults.Blocks;

namespace Common.Extensions.NpOn.ICommonDb.DbResults;

public interface INpOnRowWrapper
{
    IReadOnlyDictionary<string, INpOnCell> GetRowWrapper();
}

public interface INpOnColumnWrapper
{
    IReadOnlyDictionary<int, INpOnCell> GetColumnWrapper();
}

public interface INpOnCollectionWrapper
{
    IReadOnlyDictionary<int, INpOnColumnWrapper?> GetColumnWrapperByIndexes(int[] indexes);
    IReadOnlyDictionary<string, INpOnColumnWrapper?> GetColumnWrapperByColumnNames(string[]? columnNames = null);
    IEnumerable<string> Keys { get; }
}

public interface INpOnTableWrapper
{
    IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers { get; }
    INpOnCollectionWrapper CollectionWrappers { get; }

    /// <summary>
    /// Direct column name to index map (O(1) index lookup).
    /// Defaults to null for drivers that have not yet implemented fast array indexing.
    /// </summary>
    IReadOnlyDictionary<string, int>? NameToIndexMap => null;

    /// <summary>
    /// Direct row data storage without intermediate Cell or Dictionary wrapper allocations.
    /// Defaults to null for legacy drivers.
    /// </summary>
    IReadOnlyList<object[]>? RawData => null;

    /// <summary>
    /// Optional list of Primary Key column names.
    /// Defaults to null.
    /// </summary>
    IReadOnlyList<string>? PrimaryKeyColumns => null;

    /// <summary>
    /// Zero-Copy contiguous 1D array representing the table matrix (RowCount x ColCount).
    /// Row r, column c is located at FlatData[r * ColCount + c].
    /// Defaults to null.
    /// </summary>
    object?[]? FlatData => null;

    /// <summary>
    /// Number of active rows in the table.
    /// </summary>
    int RowCount => RawData?.Count ?? 0;

    /// <summary>
    /// Number of active columns in the table.
    /// </summary>
    int ColCount => NameToIndexMap?.Count ?? 0;

    /// <summary>
    /// Vectorized column blocks and pages (streaming batch chunks).
    /// Defaults to null.
    /// </summary>
    IReadOnlyList<NpOnDataPage>? Pages => null;
}

public interface INpOnSuperTableWrapper : INpOnTableWrapper, INpOnWrapperResult
{
}

/// <summary>
/// High-performance Tabular interface providing direct row array indexing (O(1)) without Dictionary or Cell overhead.
/// Implemented by all high-speed database/HTTP plugins.
/// </summary>
public interface IFastTabularResultSet : INpOnTableWrapper
{
    new IReadOnlyDictionary<string, int> NameToIndexMap => ((INpOnTableWrapper)this).NameToIndexMap ?? new Dictionary<string, int>();
    new IReadOnlyList<object[]> RawData => ((INpOnTableWrapper)this).RawData ?? Array.Empty<object[]>();
}
