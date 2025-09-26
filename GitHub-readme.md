## FlashTokenizer (C#)

High-performance tokenizer engine for production-scale LLM inference pipelines in .NET. Implements BERT WordPiece and GPT-2 BPE with a focus on single-threaded throughput, cache locality, and zero-allocation hot paths.

- WordPiece (BERT-style) and GPT-2 BPE parity with upstream behaviors
- Span<T>-driven text processing, ArrayPool-backed buffers, ASCII fast paths
- Optimized Aho–Corasick tries for subword matching
- Production-friendly API and console benchmark runner

Upstream project reference: [NLPOptimize/flash-tokenizer](https://github.com/NLPOptimize/flash-tokenizer/tree/main)

### Why

Tokenization is a major CPU hotspot in RAG and online inference. This project aims for sub-millisecond latency for medium inputs and >1M tokens/sec throughput without parallelization overheads.

### Status and Performance

- Input: `jazz_pakistan_faq_Copy.md` (~759k tokens)
- Earlier baseline: ~634 ms (~1.2M tokens/sec)
- Current optimized runs (Release, .NET 8, AVX2 available):
  - End-to-end Console: 759,222 tokens in 182.22 ms → ~4,166,424 tokens/sec; memory ~740 MB (process)
  - Another run on same file: 169.44 ms (tokens similar scale)
- Target: <300 ms on same input (met; now pushing further)

Measured via the console runner in Release, Server GC, ReadyToRun, TieredCompilation.

### Features

- WordPiece (BERT) and GPT-2 BPE
- Optimized basic tokenizer (`BasicTokenizerOptimized`) with zero alloc in hot paths
- ASCII-only fast path with stackalloc
- Aho–Corasick tries for initial/suffix subwords
- Optional async streaming pipeline for large files

### Quick Start

```bash
dotnet build -c Release
dotnet run -c Release --project csharp/FlashTokenizer.Console
```

Program reads a large input file and reports tokens, time, and throughput.

### API

```csharp
var tok = new FlashTokenizer.FlashTokenizer(vocabPath: "./sample/vocab.txt", doLowerCase: true, modelMaxLength: -1);
List<int> ids = tok.Encode("Hello, world.");
string text = tok.Decode(ids);
```

To use GPT-2 BPE:

```csharp
var opts = new FlashTokenizer.TokenizerOptions {
    Type = FlashTokenizer.TokenizerType.BPE,
    BpeVocabJsonPath = "./sample/vocab.json",
    BpeMergesPath = "./sample/merges.txt"
};
var tok = new FlashTokenizer.FlashTokenizer(opts);
```

### Roadmap

- SIMD-accelerated whitespace/punctuation classification
- Span<byte>-based WordPiece matching to avoid per-token UTF-8 encoding
- Flattened trie data layout and cache-friendly acceptance tables
- BenchmarkDotNet harness and perf CI

### License
### Benchmark Results (micro + end-to-end)

- WordPiece core (per token):
  - TokenizerIdsUtf8: 52.10 ns mean; 184 B/op
  - TokenizerIdsString: 137.12 ns mean; 184 B/op
  - ACTrie.Search: 20.90 ns mean; 0 B/op
- Latin-1 lower/strip micro:
  - 2.726 µs mean; 1.21 KB/op
- BasicTokenizer ASCII smoke (heavy, end-to-end small text):
  - 2.565 s mean; 740.46 MB/op (intentionally not a micro; alloc heavy)
- Real file (Console):
  - 759,222 tokens in 182.22 ms → 4.17M tokens/sec; memory ~740 MB

Notes:
- The per-token WordPiece path is ~2.6x faster via the UTF‑8 zero-copy path vs string.
- Trie search is ~21 ns/op after bitset and flat-array refactor.
- The heavy BasicTokenizer smoke test allocates by design; refer to microbenchmarks for hot-path costs.


MIT. See upstream acknowledgements in the repo and reference implementation: [NLPOptimize/flash-tokenizer](https://github.com/NLPOptimize/flash-tokenizer/tree/main).


