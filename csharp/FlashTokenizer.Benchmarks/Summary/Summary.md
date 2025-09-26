### FlashTokenizer Benchmarks Summary

1. What are benchmarks?
- Benchmarks are repeatable micro or end-to-end performance tests that measure time, throughput, and memory for critical code paths. We use BenchmarkDotNet to execute tests in isolated processes with warmup, statistical analysis, and result exports.

2. What is being benchmarked?
- TokenizerIdsUtf8 vs TokenizerIds (string): Validates the zero-copy UTF‑8 path versus the legacy string-based path in WordPiece. Confirms span/bytes path reduces overhead.
- ACTrie Search: Measures Aho–Corasick trie lookup speed after bitset refactor and flat arrays. Validates cache-local transitions and low-branch traversal.
- Latin-1 lowercasing with SIMD: Exercises the Latin‑1 fast path and SIMD-assisted checks in BasicTokenizerOptimized.
- BasicTokenizer end-to-end (ASCII sentence): Sanity check of the full ASCII path, including whitespace split and punctuation handling.

3. Benchmark results (from your latest run)
- WordpieceBenchmarks.TokenizerIdsUtf8
  - Mean: ~49.56 ns; Allocated: ~184 B per op
- WordpieceBenchmarks.TokenizerIdsString
  - Mean: ~122.38 ns; Allocated: ~184 B per op
- TrieBenchmarks.Search
  - Mean: ~21.01 ns; Allocated: 0 B per op
- SimdLatin1Benchmarks.Latin1LowerStrip
  - Mean: ~2.721 µs; Allocated: ~1.21 KB per op
- BasicTokenizerBenchmarks.TokenizeWhitespaceAndAscii
  - Initially failed due to asset path; fixed by resolving vocab via absolute path. Re-run to collect numbers.

4. What do the results tell us?
- WordPiece UTF‑8 path is ~2.5x faster than string path in tight loops (49.6 ns vs 122.4 ns). This validates the zero‑copy spans and avoiding intermediate strings.
- ACTrie search is ~21 ns/op with no allocations, confirming the bitset explicit-edge check and flat metadata improved cache locality and control flow.
- Latin‑1 lower/strip path completes in a few microseconds for a short phrase; minor per-op allocations remain from building token lists in the end-to-end call. These can be reduced further with session pooling for intermediate lists/buffers.

5. Real-world console run (provided)
- Input: 500,000 chars (sample from Simple Parallel test)
- Tokens: 122,309
- Time: 90.46 ms
- Throughput: ~1,352,150 tokens/sec

6. Remaining issues or failures
- Some benchmarks initially failed due to `sample/vocab.txt` resolution. Fixed by: absolute helper in each Setup and copying `sample` into the benchmark output. Ensure BasicTokenizerBenchmarks now uses the absolute vocab path (it does) and re-run to get numbers.

7. Repository structure (csharp/)
- FlashTokenizer: Core library (WordPiece/BPE, tries, tokenizers, utilities).
- FlashTokenizer.Benchmarks: BenchmarkDotNet suites measuring hot paths and end-to-end snippets.
- FlashTokenizer.Console: Console entry point for real-world file tokenization and throughput reporting.
- FlashTokenizer.Tests: Smoke tests for correctness.
- sample: Artifacts like vocab.txt and configs for quick runs.
- FlashTokenizer.sln: Solution file for IDE usage.

8. Usage instructions
- Build:
  - `dotnet build -c Release`
- Run Console (tokenize a file):
  - `dotnet run -c Release --project csharp/FlashTokenizer.Console`
  - Edit `Program.cs` or pass args as supported to point to your file (e.g., `C:\Users\...\jazz_pakistan_faq_Copy.md`).
- Run Benchmarks:
  - From repo root: `dotnet run -c Release -f net8.0 --project csharp/FlashTokenizer.Benchmarks`
  - Or from project dir: `cd csharp/FlashTokenizer.Benchmarks && dotnet run -c Release -f net8.0`
  - Results: `BenchmarkDotNet.Artifacts/results/*`
- Visual Studio:
  - Open `FlashTokenizer.sln`, set `FlashTokenizer.Console` as Startup, configure arguments or file paths, and debug with breakpoints in `BasicTokenizerOptimized.cs`, `ACTrie.cs`, `BpeTokenizer.cs`.

9. Next improvements
- SIMD for whitespace classification in CleanTextSpan.
- Extend Latin‑1 mapping coverage; reduce allocations in Latin‑1 path.
- Pool per-benchmark temporary lists to reduce 1.21 KB allocation in Latin‑1 micro.
- Extend BPE to reuse range/pair buffers via ArrayPool.


