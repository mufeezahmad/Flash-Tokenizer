### FlashTokenizer C# – Workspace Overview

Folders
- `FlashTokenizer`: Core library (WordPiece/BPE, tokenization, tries, utils). Build: `dotnet build -c Release`.
- `FlashTokenizer.Console`: Console runner for end-to-end tokenization of files. Run: `dotnet run -c Release --project csharp/FlashTokenizer.Console`.
- `FlashTokenizer.Benchmarks`: BenchmarkDotNet suites for hot paths and end-to-end snippets. Run: `cd csharp/FlashTokenizer.Benchmarks && dotnet run -c Release -f net8.0`.
- `FlashTokenizer.Tests`: Smoke tests.
- `sample`: Artifacts such as `vocab.txt`.

Benchmarks (highlights)
- Wordpiece `TokenizerIdsUtf8`: ~52.10 ns; `TokenizerIds(string)`: ~137.12 ns.
- ACTrie.Search: ~20.90 ns; 0 B/op.
- Latin-1 lower/strip: ~2.726 µs.
- Console (real file): 759,222 tokens in 182.22 ms → ~4.17M tokens/sec.

Notes
- Ensure .NET 8 SDK. Results are in `BenchmarkDotNet.Artifacts/results/`.

# FlashTokenizer (C#)

High-performance, fully managed .NET port of the C++ Flash Tokenizer. Supports BERT WordPiece and GPT-2 style BPE tokenization with fast UTF-8 processing, trie-accelerated matching, and optional bidirectional WordPiece fallback.

- **Author**: Mufeez Ahmad
- **License**: MIT (unless your project specifies otherwise)
- **Targets**: .NET 8.0

## Overview

This C# library mirrors the architecture and behavior of the original C++ implementation (`NLPOptimize/flash-tokenizer`) while embracing .NET best practices. It is designed for low-latency RAG and inference pipelines where tokenizer speed and accuracy are critical.

Core modules:
- `BasicTokenizer`: Text cleaning, punctuation split, Chinese spacing, optional lower/strip-accents
- `WordpieceTokenizer` and `WordpieceBackwardTokenizer`: Forward and backward WordPiece using Aho–Corasick tries
- `FlashBertTokenizer` and `FlashBertTokenizerBidirectional`: High-level BERT tokenizers
- `BpeTokenizer`: GPT-2 style BPE using `vocab.json` and `merges.txt`
- `Vocab`, `ACTrie`, `Utf8Util`, `CharMaps`, `AccentMap`: Support utilities tuned for performance
- `FlashTokenizer`: Simple facade that selects WordPiece or BPE via `TokenizerOptions`

## Features

- **WordPiece (BERT)** with fast Aho–Corasick initial/suffix tries
- **Bidirectional fallback** with `compare_ids` heuristic for robustness
- **BPE (GPT-2 style)** with `vocab.json` and `merges.txt`
- **UTF-8 aware** cleaning and decoding; Chinese spacing; punctuation handling
- **Managed-only**: No native interop; runs anywhere .NET runs

## Installation

### Option A: Local NuGet package

If you have a local `.nupkg` built for this project:

```powershell
# from your .csproj directory
dotnet add package FlashTokenizer --source .
```

Or point to a local NuGet feed/folder that contains the `.nupkg`.

### Option B: Project reference

Add the project to your solution and reference it:

```powershell
# from solution root
dotnet sln add csharp/FlashTokenizer/FlashTokenizer.csproj
# inside your app project directory
dotnet add reference ..\FlashTokenizer\FlashTokenizer.csproj
```

## Quick Start

### BERT / WordPiece

```csharp
using FlashTokenizer;

var options = new TokenizerOptions
{
	VocabPath = "path/to/vocab.txt",
	DoLowerCase = true,
	ModelMaxLength = 128,
	EnableBidirectional = false,
	Type = TokenizerType.Bert,
};

var tok = new FlashTokenizer(options);
var ids = tok.Encode("Hello, world!");
var text = tok.Decode(ids);
```

To enable bidirectional fallback:

```csharp
var tokBi = new FlashTokenizer(new TokenizerOptions
{
	VocabPath = "path/to/vocab.txt",
	EnableBidirectional = true,
	Type = TokenizerType.Bert,
});
```

### BPE (GPT-2 style)

```csharp
using FlashTokenizer;

var tok = new FlashTokenizer(new TokenizerOptions
{
	Type = TokenizerType.BPE,
	BpeVocabJsonPath = "path/to/vocab.json",
	BpeMergesPath = "path/to/merges.txt",
});

var ids = tok.Encode("The quick brown fox");
var text = tok.Decode(ids);
```

## TokenizerOptions

- `VocabPath`: string? — Path to `vocab.txt` (BERT WordPiece)
- `DoLowerCase`: bool — Lowercase and strip accents (BERT-uncased behavior)
- `ModelMaxLength`: int — Model max sequence length; `-1` for unlimited
- `EnableBidirectional`: bool — Use bidirectional WordPiece fallback
- `Type`: `TokenizerType` — `Bert` or `BPE`
- `BpeVocabJsonPath`: string? — Path to `vocab.json` (BPE)
- `BpeMergesPath`: string? — Path to `merges.txt` (BPE)

Switch algorithms via `TokenizerOptions.Type` (`TokenizerType.Bert` or `TokenizerType.BPE`).

## .NET Usage Notes

- Target .NET 8 for best performance. The library uses `ArrayBufferWriter<T>`, spans, and aggressive inlining in hot paths.
- For bulk processing, prefer `FlashBertTokenizer.BatchEncode` with `parallel: true` for throughput.
- Avoid per-call allocations by reusing data structures where possible in your integration.

## Build and Run

```powershell
# from csharp/ directory
 dotnet build FlashTokenizer.sln

# console sample
 dotnet run --project FlashTokenizer.Console -- "csharp/sample/vocab.txt" "Hello world!"
```

## Behavioral Parity Notes

- WordPiece initial/suffix trie construction mirrors C++ build order from `vocab.txt`.
- Bidirectional fallback uses a C# port of `compare_ids` heuristic to choose between forward/backward results.
- UTF-8 parsing, whitespace/control filtering, and Chinese spacing follow the C++ logic. Accent stripping mirrors the C++ map with a fallback NFKD decomposition.

If you observe mismatches on specific inputs or vocabs, please open an issue with a minimal repro (text, vocab, expected ids).

## License and Attribution

- Author: Mufeez Ahmad
- Upstream inspiration: `NLPOptimize/flash-tokenizer` (C++)
- License: MIT

## Contributing

- PRs welcome. Please include before/after perf numbers and parity checks for any behavioral changes.
- Run `dotnet build` and provide sample outputs for review.
