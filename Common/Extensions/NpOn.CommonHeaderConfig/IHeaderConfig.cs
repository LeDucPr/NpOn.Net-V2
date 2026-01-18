namespace Common.Extensions.NpOn.HeaderConfig;

public interface IHeaderConfig;

public interface IHeaderConfig<out TMetadata> : IHeaderConfig
{
    public void Replace(string key, string value);
    public TMetadata? GetHeader();
}