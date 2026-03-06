namespace Arcus.ClamAV.Benchmarks;

internal static class BenchmarkArtifacts
{
    public const string ArtifactsRootEnvVar = "CLAMAV_BENCHMARK_ARTIFACTS_ROOT";

    public static void ConfigureArtifactsRoot(string artifactsRoot)
    {
        if (string.IsNullOrWhiteSpace(artifactsRoot))
        {
            return;
        }

        Environment.SetEnvironmentVariable(ArtifactsRootEnvVar, Path.GetFullPath(artifactsRoot));
    }

    public static string GetArtifactsRoot()
    {
        var root = Environment.GetEnvironmentVariable(ArtifactsRootEnvVar);
        if (!string.IsNullOrWhiteSpace(root))
        {
            return root;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
    }

    public static string GetResultsDirectory()
    {
        return Path.Combine(GetArtifactsRoot(), "results");
    }

    public static string GetProfilesDirectory()
    {
        return Path.Combine(GetArtifactsRoot(), "profiles");
    }
}
