```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8655)
13th Gen Intel Core i9-13900, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host]          : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2
  SteadyNoTiering : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2

Job=SteadyNoTiering  EnvironmentVariables=DOTNET_TieredCompilation=0  InvocationCount=20000  
IterationCount=15  RunStrategy=Throughput  UnrollFactor=1  
WarmupCount=8  

```
| Method      | Workload   | Mean       | Error     | StdDev    | Median     | Min        | Max        | P95        | Ratio | RatioSD |
|------------ |----------- |-----------:|----------:|----------:|-----------:|-----------:|-----------:|-----------:|------:|--------:|
| **Interpreted** | **CrudFlat**   |  **0.6285 μs** | **0.0147 μs** | **0.0130 μs** |  **0.6322 μs** |  **0.5981 μs** |  **0.6476 μs** |  **0.6449 μs** |  **1.00** |    **0.03** |
| Compiled    | CrudFlat   |  0.3376 μs | 0.0124 μs | 0.0110 μs |  0.3352 μs |  0.3231 μs |  0.3624 μs |  0.3539 μs |  0.54 |    0.02 |
|             |            |            |           |           |            |            |            |            |       |         |
| **Interpreted** | **Arith100**   |  **2.9903 μs** | **0.0756 μs** | **0.0707 μs** |  **3.0100 μs** |  **2.8842 μs** |  **3.0868 μs** |  **3.0749 μs** |  **1.00** |    **0.03** |
| Compiled    | Arith100   |  0.9655 μs | 0.0320 μs | 0.0283 μs |  0.9607 μs |  0.9292 μs |  1.0219 μs |  1.0148 μs |  0.32 |    0.01 |
|             |            |            |           |           |            |            |            |            |       |         |
| **Interpreted** | **DslRich**    | **15.9805 μs** | **0.2289 μs** | **0.2029 μs** | **16.0556 μs** | **15.5292 μs** | **16.2510 μs** | **16.1683 μs** |  **1.00** |    **0.02** |
| Compiled    | DslRich    |  7.2709 μs | 0.7339 μs | 0.6865 μs |  6.9023 μs |  6.4446 μs |  8.3537 μs |  8.2708 μs |  0.46 |    0.04 |
|             |            |            |           |           |            |            |            |            |       |         |
| **Interpreted** | **Production** |  **1.9742 μs** | **0.0787 μs** | **0.0736 μs** |  **1.9472 μs** |  **1.8631 μs** |  **2.0986 μs** |  **2.0930 μs** |  **1.00** |    **0.05** |
| Compiled    | Production |  1.3104 μs | 0.0561 μs | 0.0525 μs |  1.3027 μs |  1.2339 μs |  1.4151 μs |  1.3950 μs |  0.66 |    0.04 |
