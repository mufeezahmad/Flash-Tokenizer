# ⚡ FlashTokenizer (C#)

A high-performance tokenizer engine for large-scale **LLM inference** and **RAG pipelines** in .NET.  
Implements **BERT WordPiece** and **GPT-2 BPE** with a focus on **single-threaded throughput, SIMD optimizations, cache locality, and zero-allocation hot paths**.

Ported and inspired by the upstream [NLPOptimize/flash-tokenizer (C++)](https://github.com/NLPOptimize/flash-tokenizer).

---

## ✨ Features

- 🔠 **WordPiece (BERT-style)** with forward and bidirectional variants
- 🔡 **BPE (GPT-2 style)** using `vocab.json` + `merges.txt`
- ⚡ **Span<T>-driven text processing**, `ArrayPool`-backed buffers
- 🧠 **Optimized Aho–Corasick tries** for subword matching
- 🚀 **SIMD fast paths** for ASCII casing, whitespace, punctuation
- 📊 **BenchmarkDotNet integration** for reproducible perf tests
- 🧵 Experimental **async & parallel pipelines**

---

## 📂 Repository Structure

```text
csharp/
├── FlashTokenizer/             # ✅ Core library
│   ├── WordPieceTokenizer.cs   # WordPiece + bidirectional
│   ├── BpeTokenizer.cs         # GPT-2 BPE
│   ├── ACTrie.cs               # Flattened Aho–Corasick DFA
│   ├── BasicTokenizer*.cs      # Cleaning, casing, punctuation
│   ├── Utf8Util.cs, AccentMap  # Normalization utilities
│   └── FlashTokenizer.cs       # Public facade + options
│
├── FlashTokenizer.Console/     # 🖥 Console runner (real file tokenization)
│   └── Program.cs
│
├── FlashTokenizer.Benchmarks/  # 📊 BenchmarkDotNet harness
│   ├── WordpieceBenchmarks.cs
│   ├── TrieBenchmarks.cs
│   ├── SimdLatin1Benchmarks.cs
│   ├── BasicTokenizerBenchmarks.cs
│   └── Summary.md
│
├── FlashTokenizer.Tests/       # ✅ Unit & smoke tests
│
├── sample/                     # 📄 Example vocab + input
│   └── vocab.txt
│
└── FlashTokenizer.sln          # 📦 Visual Studio 2022 solution
```

## 🚀 Quick Start

### Build & Run Console

```bash
dotnet build -c Release
dotnet run -c Release --project csharp/FlashTokenizer.Console
```

# 🛠 API Usage

## BERT / WordPiece
```csharp
var tok = new FlashTokenizer(new TokenizerOptions {
    VocabPath = "./sample/vocab.txt",
    DoLowerCase = true,
    Type = TokenizerType.Bert,
});

List<int> ids = tok.Encode("Hello, world!");
string text = tok.Decode(ids);
```

## GPT-2 BPE
```csharp
var tok = new FlashTokenizer(new TokenizerOptions {
    Type = TokenizerType.BPE,
    BpeVocabJsonPath = "./sample/vocab.json",
    BpeMergesPath = "./sample/merges.txt"
});
```

# Benchmarks
Run benchmarks:
```bash
dotnet run -c Release -f net8.0 --project csharp/FlashTokenizer.Benchmarks
```

## 📈 Results Snapshot

Below are sample benchmark results using `BenchmarkDotNet`. These may vary slightly depending on hardware, compiler, and SIMD availability.

| Benchmark                          |   Mean   |  Allocations    |
|------------------------------------|----------|-----------------|
| `WordPiece.TokenizerIdsUtf8`       | ~52 ns   | 184 B/op        |
| `WordPiece.TokenizerIdsString`     | ~137 ns  | 184 B/op        |
| `ACTrie.Search`                    | ~21 ns   | 0 B/op          |
| `Latin1.LowerStrip (SIMD)`         | ~2.7 µs  | 1.2 KB/op       |
| `Console (real file, 759k tokens)` | 112 ms | ~4.17M tok/s    |

> 🧪 Benchmarks were run in `Release` mode on .NET 8. SIMD paths auto-detect AVX2/SSE2/AdvSimd.  
> For reproducibility, run:  
> `dotnet run -c Release -f net8.0 --project csharp/FlashTokenizer.Benchmarks`
