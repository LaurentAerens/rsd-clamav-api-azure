using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace Arcus.ClamAV.Benchmarks.Profiling;

public class ProfiledBenchmarkConfig : ManualConfig
{
    public ProfiledBenchmarkConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(HtmlExporter.Default);
        AddExporter(CsvMeasurementsExporter.Default);
        AddExporter(new BenchmarkProfilerExporter());

        var artifactsRoot = BenchmarkArtifacts.GetArtifactsRoot();
        AddJob(Job.Default
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithEnvironmentVariable(BenchmarkArtifacts.ArtifactsRootEnvVar, artifactsRoot));
    }
}
