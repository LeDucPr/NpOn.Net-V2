namespace Common.Extensions.NpOn.ICommonDb.DbResults.Blocks;

public sealed class NpOnDataPage : IDisposable
{
    public int RowCount { get; }
    public int ChannelCount { get; }
    public IReadOnlyList<INpOnBlock> Blocks { get; }
    public IReadOnlyDictionary<string, int> NameToIndexMap { get; }
    private bool _disposed;

    public NpOnDataPage(int rowCount, IReadOnlyList<INpOnBlock> blocks, IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        RowCount = rowCount;
        Blocks = blocks;
        ChannelCount = blocks.Count;
        NameToIndexMap = nameToIndexMap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < Blocks.Count; i++)
        {
            Blocks[i].Dispose();
        }
    }
}

