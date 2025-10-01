# FlashTokenizer (C#) — High‑Performance WordPiece/BPE Tokenizer

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/) 

---

## Project Description

FlashTokenizer is a high‑performance, fully managed .NET implementation of fast NLP tokenization. It supports BERT WordPiece and GPT‑2 style BPE tokenization with optimized UTF‑8 text processing, trie‑accelerated subword matching, and optional bidirectional WordPiece fallback for robustness.

- **What it is**: A C#/.NET 8 library and sample apps for tokenizing text into model‑ready token IDs.
- **What it does**: Provides production‑grade APIs to encode/decode text, batch operations, and benchmark suites to measure throughput and latency.
- **Purpose & use cases**: Low‑latency inference, RAG pipelines, preprocessing in model serving, and large‑scale offline tokenization.

## Features

- Fast BERT WordPiece with Aho–Corasick tries (initial/suffix)
- Optional bidirectional fallback using a `compare_ids`‑style heuristic
- GPT‑2 style BPE with `vocab.json` + `merges.txt`
- UTF‑8 aware cleaning, punctuation splitting, Chinese spacing, accent stripping
- Optimized hot paths (spans, pooling, vectorization) for .NET 8
- Console runner and streaming/parallel tokenization demos
- BenchmarkDotNet suites and smoke tests

## Tech Stack

- **Language**: C# (net8.0)
- **Runtime**: .NET 8
- **Benchmarks**: BenchmarkDotNet
- **Packaging**: NuGet (project configured; local packaging supported)

## Repository Structure

```text
csharp/
  FlashTokenizer/                 # Core library (WordPiece/BPE, tries, utils)
    AccentMap.cs
    ACTrie.cs
    AsyncTokenizerPipeline.cs
    BasicTokenizer.cs
    BasicTokenizerOptimized.cs
    BpeTokenizer.cs
    FlashBertTokenizer*.cs        # BERT/tokenizer variants (optimized, parallel, bidirectional)
    FlashTokenizer.cs             # Public facade for WordPiece/BPE
    Heuristics.cs
    ITokenizer.cs
    TokenizerOptions.cs
    TokenizerSession.cs
    TokenizerType.cs
    Utf8Util.cs
    Vocab.cs
    Wordpiece*Tokenizer.cs
    upstream_charmap.h            # Embedded char map resource

  FlashTokenizer.Console/         # Console runner and perf demos
    Program.cs
    SimpleTest.cs

  FlashTokenizer.Benchmarks/      # BenchmarkDotNet suites
    Program.cs
    BasicTokenizerBenchmarks.cs
    SimdLatin1Benchmarks.cs
    TrieBenchmarks.cs
    WordpieceBenchmarks.cs

  FlashTokenizer.Tests/           # Minimal smoke tests
    SmokeTests.cs

  README.md                       # Workspace overview (in csharp/)
  FlashTokenizer.sln              # Solution file

sample/                           # Example vocab/config assets
  vocab.txt
  tokenizer_config.json
```

### What the major parts do
- `csharp/FlashTokenizer/`: main library – tokenizers, tries, char maps, utilities, and public facade `FlashTokenizer`.
- `csharp/FlashTokenizer.Console/`: runs optimized, parallel, or async streaming tokenization on a file for quick performance checks.
- `csharp/FlashTokenizer.Benchmarks/`: micro/meso benchmarks for hot paths and E2E tokenization.
- `csharp/FlashTokenizer.Tests/`: smoke test harness demonstrating encode/tokenize calls.
- `sample/`: example `vocab.txt` and configs for quick experiments.

## Installation

Prerequisites:
- .NET 8 SDK

Options:

1) Project reference (recommended during development)

```powershell
# from repository root
dotnet sln add csharp/FlashTokenizer/FlashTokenizer.csproj
# inside your app project
dotnet add reference ..\FlashTokenizer\FlashTokenizer.csproj
```

2) Local NuGet package (if you build one)

```powershell
# from your app project's directory
dotnet add package FlashTokenizer --source .
```

## Usage

### Library (BERT / WordPiece)

```csharp
using FlashTokenizer;

var tok = new FlashTokenizer(new TokenizerOptions
{
    VocabPath = "path/to/vocab.txt",
    DoLowerCase = true,
    ModelMaxLength = 128,
    EnableBidirectional = false,
    Type = TokenizerType.Bert,
});

var ids = tok.Encode("Hello, world!");
var text = tok.Decode(ids);
```

Enable bidirectional fallback:

```csharp
var tokBi = new FlashTokenizer(new TokenizerOptions
{
    VocabPath = "path/to/vocab.txt",
    EnableBidirectional = true,
    Type = TokenizerType.Bert,
});
```

### Library (GPT‑2 BPE)

```csharp
var tok = new FlashTokenizer(new TokenizerOptions
{
    Type = TokenizerType.BPE,
    BpeVocabJsonPath = "path/to/vocab.json",
    BpeMergesPath = "path/to/merges.txt",
});

var ids = tok.Encode("The quick brown fox");
var text = tok.Decode(ids);
```

### Console runner

The console app demonstrates multiple modes: optimized sequential, simple parallel, and async streaming.

```powershell
# from csharp/ directory
dotnet run --project FlashTokenizer.Console -- "csharp/sample/vocab.txt" optimized
dotnet run --project FlashTokenizer.Console -- "csharp/sample/vocab.txt" parallel
dotnet run --project FlashTokenizer.Console -- "csharp/sample/vocab.txt" async
dotnet run --project FlashTokenizer.Console -- "csharp/sample/vocab.txt" compare
```

If no mode is supplied, the optimized benchmark runs by default. The console uses a sample `vocab.txt` path if none is provided.

## Testing

This repository includes a minimal smoke test harness:

```csharp
// csharp/FlashTokenizer.Tests/SmokeTests.cs
var vocabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt"));
var tok = new FlashBertTokenizer(vocabPath, doLowerCase: true, modelMaxLength: 16);
var ids = tok.Encode("Hello world!");
```

Run any test harness you create by referencing the library; smoke tests can be invoked by calling `SmokeTests.Run()` from your own host/test runner.

Benchmarks use BenchmarkDotNet:

```powershell
cd csharp/FlashTokenizer.Benchmarks
dotnet run -c Release -f net8.0
```

Results are written under `BenchmarkDotNet.Artifacts/results/`.

## Configuration

`TokenizerOptions` (for the `FlashTokenizer` facade):

| Option | Type | Description |
|-------|------|-------------|
| `VocabPath` | string? | Path to `vocab.txt` (WordPiece). |
| `DoLowerCase` | bool | Apply lowercasing and accent stripping. |
| `ModelMaxLength` | int | Max sequence length; `-1` for unlimited. |
| `EnableBidirectional` | bool | Enables bidirectional WordPiece fallback. |
| `Type` | `TokenizerType` | `Bert` or `BPE`. |
| `BpeVocabJsonPath` | string? | Path to BPE `vocab.json`. |
| `BpeMergesPath` | string? | Path to BPE `merges.txt`. |

Defaults: WordPiece, lowercase enabled, `ModelMaxLength = 128`. If `VocabPath` is not set, examples fall back to the repo `sample/vocab.txt`.

## Build & Deployment

Build everything:

```powershell
cd csharp
dotnet build FlashTokenizer.sln -c Release
```

## Authentication/Security

No authentication or external security surfaces are included. The library operates on local data and does not perform network I/O.

## Architecture (high level)

- **Text preprocessing**: `BasicTokenizer` / `BasicTokenizerOptimized` performs cleaning, punctuation splitting, Chinese spacing, and optional lower/strip accents (SIMD‑optimized in Latin‑1 path).
- **Subword matching (WordPiece)**: `ACTrie`‑based initial and suffix tries drive greedy longest‑match. `FlashBertTokenizer` coordinates encode/decode; `FlashBertTokenizerBidirectional` provides a fallback that compares forward/backward segmentations.
- **BPE**: `BpeTokenizer` loads `vocab.json` and `merges.txt`, then applies GPT‑2 style merges.
- **Facade**: `FlashTokenizer` selects WordPiece or BPE via `TokenizerOptions` and exposes simple `Encode`/`Decode` methods.

## Acknowledgements

- Inspired by the upstream C++ project [NLPOptimize/flash-tokenizer (C++)](https://github.com/NLPOptimize/flash-tokenizer)



