## FlashTokenizer (C#) – Technical Deep Dive

This document explains the architecture, performance profile, and optimization techniques used in the C# FlashTokenizer implementation. It is intended for maintainers and performance engineers.

Upstream reference: [NLPOptimize/flash-tokenizer](https://github.com/NLPOptimize/flash-tokenizer/tree/main)

### Project Structure

- `csharp/FlashTokenizer/`
  - `FlashTokenizer.cs`: public facade, selects WordPiece or BPE
  - `BasicTokenizerOptimized.cs`: span-based whitespace/punctuation tokenization
  - `WordpieceTokenizer.cs`: subword segmentation using Aho–Corasick tries
  - `ACTrie.cs`: compact AC automaton with flattened DFA for cache locality
  - `BpeTokenizer.cs`: GPT‑2 byte-level BPE with canonical regex and tables
  - `Utf8Util.cs`, `AccentMap.cs`, `CharMaps.cs`: utilities for Unicode/ASCII fast paths
  - `FlashBertTokenizer*.cs`: higher-level orchestrators and variants
- `csharp/FlashTokenizer.Console/Program.cs`: benchmark harness
- `csharp/FlashTokenizer.Tests/`: smoke tests

### Pipeline Overview (WordPiece)

1. UTF‑8 encode input once; clean/control filtering; Chinese spacing (span-based)
2. Whitespace split into tokens (span-based)
3. Per-token fast path:
   - ASCII-only → stackalloc-lowered casing → punctuation split in bytes
   - Non-ASCII → Unicode lower+accent strip → punctuation split via codepoints
4. Subword matching via AC tries (initial vs suffix) → vocab ids

### Current Performance

- Input: `jazz_pakistan_faq_Copy.md` (~759k tokens)
- Console (Release, .NET 8): 759,222 tokens in 182.22 ms → ~4.17M tokens/sec; memory ~740 MB
- Additional run recorded 169.44 ms on same file (similar token count)
- Previous parallel attempts regressed due to overhead and contention.

### Hotspots and Bottlenecks

- UTF‑8 conversions in `WordpieceTokenizer.TokenizerIds` per token
- `Encoding.UTF8.GetString` across punctuation splits (multiple short string allocations)
- AC trie traversal per token from `start=0` even for long tokens
- Regex cost in BPE (compiled, but still heavy for large texts)

### Key Optimizations Implemented

- ArrayPool-backed UTF‑8 buffers for initial encode and cleaning
- ASCII fast path with stackalloc and in-place lowercasing
- Flattened AC DFA and explicit edge tracking for early exit
- Minimal branching and aggressive inlining on hot paths

### Proposed Improvements (Single-threaded)

1. Avoid per-token UTF‑8 encode in `TokenizerIds`:
   - Accept `ReadOnlySpan<byte>` variant: `TokenizerIdsUtf8(ReadOnlySpan<byte> tokenBytes, ...)` to pass bytes directly from basic tokenizer for ASCII and pre-encoded paths.
   - Keep a slow path for non-ASCII requiring normalization.
2. SIMD classification for whitespace and punctuation:
   - Use `System.Numerics.Vector<byte>` or `AdvSimd/Sse2` via `Vector.IsHardwareAccelerated` for block scanning.
3. Lower/strip accents using lookup tables for BMP where possible:
   - Precompute ASCII/Latin-1 case/diacritic maps to avoid full `string` round-trips.
4. AC trie layout tuning:
   - Replace `bool[] Explicit` with bitsets (e.g., `ulong[4]`) to shrink footprint and improve cache residency.
   - Store `VocabIndex` and `WordLen` in parallel arrays (struct-of-arrays) to reduce node size.
5. Reduce string allocations in punctuation split:
   - Emit subword ids directly from byte spans when ASCII; only materialize string for non-ASCII/normalization.
6. Add `List<int>` growth policy:
   - Pre-size based on heuristic of tokens-per-char to minimize resizes.
7. Streaming I/O for very large inputs:
   - Async pipeline is optional; for single-threaded streaming, process chunks with boundary carry-over of partial tokens.

### Benchmarking
#### Results Snapshot

- WordpieceBenchmarks
  - TokenizerIdsUtf8: 52.10 ns mean; 184 B/op
  - TokenizerIdsString: 137.12 ns mean; 184 B/op
- TrieBenchmarks.Search: 20.90 ns mean; 0 B/op
- SimdLatin1Benchmarks.Latin1LowerStrip: 2.726 µs mean; 1.21 KB/op
- BasicTokenizerBenchmarks.TokenizeWhitespaceAndAscii: 2.565 s mean; 740.46 MB/op (end-to-end small sample, not a micro)
- End-to-end Console: 759,222 tokens in 182.22 ms → 4.17M tokens/sec


- Add BenchmarkDotNet micro-benchmarks for:
  - CleanTextSpan, TokenizeWhitespaceSpan, ProcessAsciiToken, SplitOnPuncBytes
  - ACTrie.Search on synthetic distributions
  - WordpieceTokenizer.TokenizerIds on varying token lengths

Sample harness skeleton is provided in repo root README.

### Compatibility Notes

- Behavior matches upstream WordPiece and GPT‑2 BPE expectations. Refer to upstream docs for CUDA/GPU variants and broader ecosystem notes: [NLPOptimize/flash-tokenizer](https://github.com/NLPOptimize/flash-tokenizer/tree/main).

### License and Acknowledgements

MIT. Inspired by FlashAttention/FlashInfer style optimization techniques and the upstream FlashTokenizer (C++). Also references HuggingFace tokenizers and OpenAI GPT‑2 byte encodings.


### SIMD Optimizations (2025-09)

We integrated SIMD into core hot paths for cross-platform speedups with hardware fallbacks:

- Whitespace split over UTF-8 bytes now uses block classification with AVX2/SSE2 when available, falling back to scalar.
- ASCII lowercasing path in `ProcessAsciiToken` uses vector masks to add 32 to lanes in the A..Z range, with scalar tail.
- ASCII punctuation detection already leveraged vector scans; minor cleanups retained.
- Latin-1 lower/strip and Unicode paths remain with lookup tables and scalar loops; further SIMDization is planned.

Hardware checks ensure safety on older CPUs. All SIMD code paths keep scalar fallbacks. Target framework: .NET 8.

#### SIMD Coverage

| Area | Current | SIMD Potential | Action |
|---|---|---|---|
| Whitespace split | Scalar loop | SIMD vector compare | Rewritten with Intrinsics |
| Lowercase ASCII | Scalar loop | SIMD range/mask/add | Rewritten with AVX2/SSE2 + fallback |
| Accent strip Latin-1 | Loop | SIMD lookup/mask | Pending (lookup batching) |
| Punctuation split (ASCII) | SIMD | SIMD Unicode | Pending (Unicode extension) |
| Token filtering/cleaning | Scalar loop | SIMD mask/copy | Pending |
| Unicode property checks | Scalar | SIMD batch | Feasibility under review |

### Build and Run Commands

- Run benchmarks:

```bash
dotnet run -c Release -f net8.0 --project csharp/FlashTokenizer.Benchmarks
```

- Run console benchmark:

```bash
dotnet run -c Release --project csharp/FlashTokenizer.Console
```

- If building from project root (Windows example):

```powershell
cd "C:\\Users\\Mufeez.Ahmad\\Downloads\\flash-tokenizer-main-C#"
dotnet run -c Release --project csharp/FlashTokenizer.Console
```


