```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8655)
13th Gen Intel Core i9-13900, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2

Job=SteadyNoTiering  EnvironmentVariables=DOTNET_TieredCompilation=0  Toolchain=InProcessEmitToolchain  
InvocationCount=2000  IterationCount=15  RunStrategy=Throughput  
UnrollFactor=1  WarmupCount=8  

```
| Method | Mean      | Error     | StdDev    | Median    | P95       |
|------- |----------:|----------:|----------:|----------:|----------:|
| Verb   | 0.3335 μs | 0.0039 μs | 0.0031 μs | 0.3330 μs | 0.3382 μs |
