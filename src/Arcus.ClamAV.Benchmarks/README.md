# Performance Profiling & Optimization Guide

This directory contains tools to **measure, visualize, and optimize** the ClamAV API's performance.

---

## 🎯 Quick Start: Function-Level Timing Report (Simplest)

Get a function-level timing report showing which methods take the most time:

```powershell
cd src/Arcus.ClamAV.Benchmarks
dotnet run -c Release -- --single-request --payload large
```

**Output:**
```
Performance Profile Report
=========================
Total Execution Time: 457.00 ms

Method                                                Calls   Total (ms)     Avg (ms)     Min (ms)     Max (ms)   % Time
--------------------------------------------------------------------------------------------------------------------------
JsonScanHandler.HandleAsync                               1       235.00       235.00          235          235   51.42%
ISyncScanService.ScanStreamAsync                         15       218.00        14.53           12           16   47.70%
IJsonBase64ExtractorService.ExtractBase64Properties       1         4.00         4.00            4            4    0.88%
```

**Files saved:**
- `BenchmarkDotNet.Artifacts/profiles/profile-large-*.txt` - Human-readable report
- `BenchmarkDotNet.Artifacts/profiles/profile-large-*.json` - Machine-readable JSON for automation
- `BenchmarkDotNet.Artifacts/profiles/profile-large-*.csv` - CSV for spreadsheet analysis

**Payload options:**
- `--payload large` - ~500KB JSON with 3 base64 fields (default)
- `--payload small` - Small JSON payload
- `--payload mixed` - Mixed payload with EICAR-infected base64

**Use this when:** You want to quickly identify which specific functions are bottlenecks (no external tools required)

---

## 🎯 Statistical Benchmarks (HTML + Charts)

Run benchmarks to get **visual comparison charts**:

```powershell
cd src/Arcus.ClamAV.Benchmarks
dotnet run -c Release
```

**Output:**
- `BenchmarkDotNet.Artifacts/results/*.html` - Interactive HTML reports with charts
- `BenchmarkDotNet.Artifacts/results/*-report.csv` - Raw CSV data
- Console summary with performance comparisons

**Shows:** Throughput, memory allocation, GC pressure comparisons

---

### Option 2: Flame Graphs (Call Tree Visualization)

Profile the **running application** to see where time is spent:

```powershell
cd src/Arcus.ClamAV.Benchmarks
.\profile-app.ps1 -Duration 30
```

**Generates:**
- **Speedscope flame graph** - Upload to https://www.speedscope.app/
- Shows exact function call hierarchy
- Identifies CPU-intensive code paths

**Alternative formats:**
```powershell
# Open in Visual Studio
.\profile-app.ps1 -OutputFormat nettrace

# Open in Chrome DevTools
.\profile-app.ps1 -OutputFormat chromium
```

---

### Option 3: Visual Studio Profiler (If Available)

1. Open `Arcus.ClamAV.sln` in **Visual Studio 2022+**
2. Select **Debug → Performance Profiler**
3. Choose **CPU Usage** + **Memory Allocation**
4. Run profile session
5. Analyze hot paths in visual call tree

**Best for:** Detailed source-level analysis with line-by-line timing

---

## 📊 Current Performance Baseline

From latest benchmark run:

| Scenario | Time | Memory | Key Insight |
|----------|------|--------|-------------|
| Small JSON (~20KB) | **77 ms** | 83 KB | Moderate overhead |
| Large JSON (~500KB) | **231 ms** | 5.1 MB | **ClamAV scanning bottleneck** |
| Infected (early exit) | **31 ms** | 1.3 MB | 7× faster |
| Extraction only | **1.3 ms** | 1.7 MB | **Not the bottleneck!** |

### Key Finding

**JSON parsing is fast (1.3ms)** – the bottleneck is **ClamAV scanning (~230ms per large file)**.

To reach 150 calls/sec:
- ✅ C# code is optimized
- ⚠️ Focus on ClamAV configuration tuning
- ⚠️ Increase `MaxThreads` in clamd.conf
- ⚠️ Scale horizontally with more replicas

---

## 🔧 Optimization Workflow

### 1. **Measure**
```powershell
# Run benchmarks to establish baseline
dotnet run -c Release
```

### 2. **Profile**
```powershell
# Capture call tree while under load
.\profile-app.ps1 -Duration 60
```

### 3. **Analyze**
- Open speedscope visualization
- Find functions consuming >10% of total time
- Check for:
  - Blocking I/O operations
  - Excessive allocations
  - Unnecessary serialization
  - Lock contention

### 4. **Optimize**
- Focus on top 20% of hot paths (Pareto principle)
- Make code changes

### 5. **Verify**
```powershell
# Re-run benchmarks to confirm improvement
dotnet run -c Release
```

---

## 🧪 Benchmark Scenarios

### JsonExtractorBenchmarks
- **Tests:** Base64 extraction speed from JSON
- **Payloads:** Small, large, mixed clean+infected
- **Measures:** Time, memory allocation, GC pressure

### JsonScanHandlerBenchmarks
- **Tests:** Full handler pipeline (extract → scan → respond)
- **Scenarios:**
  - Small clean payload (~20KB)
  - Large clean payload (~500KB)
  - Mixed infected (early exit)
  - Extraction-only (no scanning)

---

## 📈 Interpreting Results

### Benchmark Output

```
| Method                  | Mean    | Allocated |
|------------------------ |--------:|----------:|
| ScanSmallClean          | 77.4 ms |    82 KB  |
| ScanLargeClean          | 231 ms  |  5132 KB  |
| ExtractOnly_LargePayload| 1.3 ms  |  1745 KB  |
```

**Look for:**
- **Mean:** Average execution time (lower = better)
- **Allocated:** Memory allocated per operation (lower = better)
- **Gen0/Gen1/Gen2:** Garbage collection frequency (avoid Gen2)

### Speedscope Flame Graph

- **Width:** Time spent in function (wider = more time)
- **Depth:** Call stack depth
- **Color:** Different call paths

**Hot path example:**
```
JsonScanHandler.HandleAsync (230ms)
  └─ SyncScanService.ScanStreamAsync (228ms)  ← BOTTLENECK
       └─ ClamAV.SendStreamAsync (225ms)
```

---

## 🎯 Optimization Targets

Based on current analysis:

### ✅ Already Optimized
- JSON parsing (1.3ms for 500KB)
- Base64 extraction
- Handler overhead

### ⚠️ Needs Attention
1. **ClamAV scanning time** (230ms per large file)
   - Check clamd.conf settings
   - Increase MaxThreads
   - Tune StreamMaxLength
   - Consider signature database size

2. **Concurrent capacity**
   - Current: 4 clamd workers
   - Target: 35+ workers for 150 req/sec
   - Options: Scale horizontally or increase workers

---

## 🔗 Tools Reference

- **BenchmarkDotNet:** https://benchmarkdotnet.org/
- **dotnet-trace:** https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace
- **Speedscope:** https://www.speedscope.app/
- **PerfView:** https://github.com/microsoft/perfview
- **Visual Studio Profiler:** Built into VS 2022+

---

## 📝 Adding New Benchmarks

```csharp
[Benchmark(Description = "My new benchmark")]
public async Task MyNewBenchmark()
{
    // Code to benchmark
    await _handler.HandleAsync(_testPayload);
}
```

Then re-run: `dotnet run -c Release`
