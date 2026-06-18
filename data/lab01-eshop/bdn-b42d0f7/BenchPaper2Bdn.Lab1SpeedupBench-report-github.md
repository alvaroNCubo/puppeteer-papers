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
| Method      | Workload   | Mean       | Error     | StdDev    | Median     | Min        | Max       | P95       | Ratio | RatioSD |
|------------ |----------- |-----------:|----------:|----------:|-----------:|-----------:|----------:|----------:|------:|--------:|
| **Interpreted** | **Arith100**   |  **3.0343 μs** | **0.0809 μs** | **0.0717 μs** |  **3.0318 μs** |  **2.9159 μs** |  **3.166 μs** |  **3.150 μs** |  **1.00** |    **0.03** |
| Compiled    | Arith100   |  0.9884 μs | 0.0249 μs | 0.0221 μs |  0.9811 μs |  0.9566 μs |  1.033 μs |  1.029 μs |  0.33 |    0.01 |
|             |            |            |           |           |            |            |           |           |       |         |
| **Interpreted** | **DslRich**    | **15.4226 μs** | **1.3363 μs** | **1.2500 μs** | **14.8200 μs** | **14.4394 μs** | **17.918 μs** | **17.736 μs** |  **1.01** |    **0.11** |
| Compiled    | DslRich    |  6.0463 μs | 0.0886 μs | 0.0829 μs |  6.0346 μs |  5.8833 μs |  6.199 μs |  6.167 μs |  0.39 |    0.03 |
|             |            |            |           |           |            |            |           |           |       |         |
| **Interpreted** | **Production** |  **1.8715 μs** | **0.0233 μs** | **0.0207 μs** |  **1.8736 μs** |  **1.8215 μs** |  **1.906 μs** |  **1.899 μs** |  **1.00** |    **0.02** |
| Compiled    | Production |  1.1735 μs | 0.0188 μs | 0.0167 μs |  1.1737 μs |  1.1498 μs |  1.201 μs |  1.195 μs |  0.63 |    0.01 |
