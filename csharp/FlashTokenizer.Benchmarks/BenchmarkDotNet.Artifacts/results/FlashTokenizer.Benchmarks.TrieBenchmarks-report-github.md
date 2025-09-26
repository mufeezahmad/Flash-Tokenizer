```

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.6093/22H2/2022Update)
Intel Core i5-6500T CPU 2.50GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.411
  [Host]     : .NET 8.0.17 (8.0.1725.26602), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.17 (8.0.1725.26602), X64 RyuJIT AVX2


```
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Search | 20.98 ns | 0.086 ns | 0.080 ns |         - |
