# FlashTokenizer (C#) — High‑Performance WordPiece/BPE Tokenizer

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/) 
[![NuGet](https://img.shields.io/nuget/v/FlashTokenizer)](https://www.nuget.org/packages/FlashTokenizer/) 
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) 
[![Performance](https://img.shields.io/badge/performance-12.7M%20tokens%2Fsec-brightgreen)](https://github.com/mufeezahmad/flash-tokenizer)
---

## Project Description

FlashTokenizer is a high‑performance, fully managed .NET implementation of fast NLP tokenization. It supports BERT WordPiece and GPT‑2 style BPE tokenization with optimized UTF‑8 text processing, trie‑accelerated subword matching, and optional bidirectional WordPiece fallback for robustness.

### What it does
- **Tokenizes text** into model‑ready tokens (strings or IDs)
- **Processes large documents** with parallel and async processing  
- **Optimizes performance** with SIMD acceleration and memory pooling
- **Supports multiple algorithms** (BERT WordPiece, GPT-2 BPE)
- **Handles Unicode properly** with UTF-8 optimization and accent stripping

### Use cases
- **AI/ML Pipelines**: BERT, GPT, transformer model preprocessing
- **Data Processing**: Large‑scale text analysis and ETL workflows
- **Search Systems**: Text indexing and retrieval applications  
- **NLP Applications**: Chatbots, sentiment analysis, text classification
- **Document Processing**: Academic papers, legal documents, content analysis

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

### Via NuGet (recommended)

```powershell
dotnet add package FlashTokenizer
```

### Options:

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

### Quick Start (NuGet Package)

```csharp
using FlashTokenizer;

// Simple string tokenization
var tokenizer = new Tokenizer(doLowerCase: true);
List<string> tokens = tokenizer.Tokenize("Hello, world!");

// High-performance BERT WordPiece (recommended for production)
var bertTokenizer = new FlashBertTokenizerOptimized(
    vocabPath: "path/to/vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1  // unlimited length
);
var tokenIds = bertTokenizer.Encode("Hello, world!", padding: "longest", maxLength: -1);
```
### Available Tokenizer Modes

#### 1. **Simple String Tokenizer** (Basic preprocessing)
```csharp
var tokenizer = new Tokenizer(doLowerCase: true, tokenizeChineseChars: true);
List<string> tokens = tokenizer.Tokenize("Hello, world!");
```

#### 2. **High-Performance BERT** (Recommended for large documents)
```csharp
var tokenizer = new FlashBertTokenizerOptimized(
    vocabPath: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,  // unlimited
    tokenizeChineseChars: true
);
var ids = tokenizer.Encode(text, padding: "longest", maxLength: -1);
```

#### 3. **Parallel BERT** (Multi-threaded for huge files)
```csharp
var tokenizer = new FlashBertTokenizerParallel(
    vocabPath: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,
    tokenizeChineseChars: true,
    maxDegreeOfParallelism: Environment.ProcessorCount,
    chunkSize: 256 * 1024
);
var ids = tokenizer.Encode(largeText);
```

#### 4. **Async Streaming** (For file processing)
```csharp
using var pipeline = new AsyncTokenizerPipeline(
    vocabPath: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,
    tokenizeChineseChars: true,
    maxDegreeOfParallelism: Environment.ProcessorCount,
    chunkSize: 128 * 1024,
    bufferSize: 1024 * 1024
);
var ids = await pipeline.ProcessFileAsync("large_file.txt");
```

#### 5. **Bidirectional BERT** (Robust fallback)
```csharp
var tokenizer = new FlashBertTokenizerBidirectional(
    vocabPath: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1
);
var ids = tokenizer.Encode(text, padding: "longest", maxLength: -1);
```

#### 6. **BPE (GPT-2 style)**
```csharp
var tokenizer = new BpeTokenizer(
    vocabJsonPath: "vocab.json",
    mergesPath: "merges.txt"
);
var ids = tokenizer.Encode("The quick brown fox");
```

#### 7. **Unified Facade** (Auto-selects algorithm)
```csharp
// BERT WordPiece
var tok = new FlashTokenizer(new TokenizerOptions
{
    VocabPath = "vocab.txt",
    DoLowerCase = true,
    ModelMaxLength = -1,  // unlimited
    EnableBidirectional = false,
    Type = TokenizerType.Bert
});

// BPE
var bpeTok = new FlashTokenizer(new TokenizerOptions
{
    Type = TokenizerType.BPE,
    BpeVocabJsonPath = "vocab.json",
    BpeMergesPath = "merges.txt"
});
```

### Performance Recommendations

- **Small texts** (< 1KB): Use `Tokenizer` or `FlashBertTokenizer`
- **Medium documents** (1KB - 1MB): Use `FlashBertTokenizerOptimized`
- **Large files** (> 1MB): Use `FlashBertTokenizerParallel` or `AsyncTokenizerPipeline`
- **Robust processing**: Add `EnableBidirectional = true` or use `FlashBertTokenizerBidirectional`

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

## Current Performance

- Input: `jazz_pakistan_faq_Copy.md` (~759k tokens) | File Size 4 M.B Approx
- Console (Release, .NET 8): `759,222 tokens` in `110.22 ms` → ~6.89M tokens/sec; memory ~740 MB
- On Parallel Mode: 759,222 tokens in less than `60 ms` → ~12.65M tokens/sec;

## Build & Deployment

Build everything:

```powershell
cd csharp
dotnet build FlashTokenizer.sln -c Release
```

Create a NuGet package from the library project:

```powershell
cd csharp/FlashTokenizer
dotnet pack -c Release
```

The package will be generated under `csharp/FlashTokenizer/bin/Release/`.


## Architecture (high level)

- **Text preprocessing**: `BasicTokenizer` / `BasicTokenizerOptimized` performs cleaning, punctuation splitting, Chinese spacing, and optional lower/strip accents (SIMD‑optimized in Latin‑1 path).
- **Subword matching (WordPiece)**: `ACTrie`‑based initial and suffix tries drive greedy longest‑match. `FlashBertTokenizer` coordinates encode/decode; `FlashBertTokenizerBidirectional` provides a fallback that compares forward/backward segmentations.
- **BPE**: `BpeTokenizer` loads `vocab.json` and `merges.txt`, then applies GPT‑2 style merges.
- **Facade**: `FlashTokenizer` selects WordPiece or BPE via `TokenizerOptions` and exposes simple `Encode`/`Decode` methods.

## Acknowledgements

- Inspired by the upstream C++ project [NLPOptimize/flash-tokenizer (C++)](https://github.com/NLPOptimize/flash-tokenizer)
