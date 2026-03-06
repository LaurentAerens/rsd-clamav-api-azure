namespace Arcus.ClamAV.Services;

public interface IPerformanceProfiler
{
    IDisposable Track(string name);
}

public sealed class NoOpPerformanceProfiler : IPerformanceProfiler
{
    public static readonly NoOpPerformanceProfiler Instance = new();

    private static readonly IDisposable NoOpScopeInstance = new NoOpScope();

    private NoOpPerformanceProfiler()
    {
    }

    public IDisposable Track(string name)
    {
        return NoOpScopeInstance;
    }

    private sealed class NoOpScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
