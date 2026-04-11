using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Npgsql;

namespace Common.Infrastructures.NpOn.YugaByteExtCm.Results;

public class YugaByteResultSetWrapper : PostgresResultSetWrapper
{
    public YugaByteResultSetWrapper() : base()
    {
    }

    public YugaByteResultSetWrapper(NpgsqlDataReader reader) : base(reader)
    {
    }
}
