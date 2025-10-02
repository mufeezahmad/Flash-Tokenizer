# FlashTokenizer NuGet Package Guide

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4) ![NuGet](https://img.shields.io/nuget/v/FlashTokenizer) ![License](https://img.shields.io/badge/license-MIT-blue.svg)

## About FlashTokenizer

**FlashTokenizer** is a high-performance, production-ready tokenization library for .NET 8 applications. It provides blazing-fast implementations of popular tokenization algorithms including BERT WordPiece and GPT-2 style BPE (Byte Pair Encoding).

### What is Tokenization?

Tokenization is the process of breaking down text into smaller units (tokens) that machine learning models can understand. These tokens can be:
- **Words or subwords** (like "hello", "world")
- **Token IDs** (numerical representations like [101, 7592, 2088, 102])

### Why FlashTokenizer?

**Performance**: Up to **12.7M tokens/sec** throughput  
**Flexible**: 8 different tokenizer classes for various use cases  
**Optimized**: SIMD acceleration, parallel processing, async streaming  
**Production-Ready**: Memory efficient, well-tested, comprehensive documentation  
**Multi-Language**: Supports Chinese, multilingual text processing  
**Easy Integration**: Simple NuGet package, clean APIs

### Key Features

- **BERT WordPiece**: Fast subword tokenization with Aho-Corasick tries
- **GPT-2 BPE**: Byte Pair Encoding for transformer models  
- **Parallel Processing**: Multi-threaded tokenization for large documents
- **Async Streaming**: Memory-efficient file processing
- **Bidirectional Fallback**: Improved quality with dual-direction tokenization
- **UTF-8 Optimized**: Proper Unicode handling and accent stripping
- **Configurable**: Extensive options for different use cases

### Use Cases

- **AI/ML Pipelines**: Preprocessing for BERT, GPT, and transformer models
- **Data Processing**: Large-scale text analysis and ETL workflows  
- **Search Systems**: Text indexing and retrieval applications
- **NLP Applications**: Chatbots, sentiment analysis, text classification
- **Document Processing**: Academic papers, legal documents, content analysis
- **Multilingual Systems**: International text processing workflows

## Installation

```bash
dotnet add package FlashTokenizer
```

Or via Package Manager Console in Visual Studio:
```powershell
Install-Package FlashTokenizer
```

## Quick Start

### Basic Usage
```csharp
using FlashTokenizer;

// Simple string tokenization
var tokenizer = new Tokenizer();
List<string> tokens = tokenizer.Tokenize("Hello, world!");

// BERT WordPiece tokenization  
var bertTokenizer = new FlashBertTokenizerOptimized("vocab.txt");
List<int> ids = bertTokenizer.Encode("Hello, world!");
```

## Available Tokenizer Classes

### 1. `Tokenizer` - Simple String Tokenization

Basic text preprocessing that returns string tokens.

```csharp
var tokenizer = new Tokenizer(
    doLowerCase: true,           // Convert to lowercase
    tokenizeChineseChars: true   // Add spaces around CJK characters
);

List<string> tokens = tokenizer.Tokenize("Hello, 世界!");
// Output: ["hello", ",", "世", "界", "!"]
```

**Use cases:**
- Text preprocessing
- Simple tokenization without subword splitting
- When you need string tokens, not IDs

---

### 2. `FlashBertTokenizer` - Standard BERT WordPiece

Basic BERT tokenizer with WordPiece algorithm.

```csharp
var tokenizer = new FlashBertTokenizer(
    vocabFile: "path/to/vocab.txt",
    doLowerCase: true,
    modelMaxLength: 512,         // Standard BERT length
    tokenizeChineseChars: true
);

// Encode text to token IDs
List<int> ids = tokenizer.Encode("Hello, world!");

// Decode back to text
string text = tokenizer.Decode(ids);

// With explicit parameters
List<int> ids2 = tokenizer.Encode(
    text: "Hello, world!",
    padding: "max_length",      // "max_length" or "longest"
    maxLength: 512
);
```

**Use cases:**
- Standard BERT tokenization
- Small to medium texts
- When you need basic WordPiece functionality

---

### 3. `FlashBertTokenizerOptimized` - High-Performance BERT

Optimized version with better performance for production use.

```csharp
var tokenizer = new FlashBertTokenizerOptimized(
    vocabFile: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,          // -1 = unlimited length
    tokenizeChineseChars: true
);

// For large documents, use unlimited length
List<int> ids = tokenizer.Encode(
    text: largeDocument,
    padding: "longest",          // No padding for large docs
    maxLength: -1               // Unlimited
);
```

**Performance tips:**
```csharp
// Warmup for consistent benchmarking
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
var warmupIds = tokenizer.Encode(text.Substring(0, Math.Min(1000, text.Length)));

// Actual tokenization
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
var stopwatch = Stopwatch.StartNew();
var ids = tokenizer.Encode(text, "longest", -1);
stopwatch.Stop();
```

**Use cases:**
- Production applications
- Large documents (1KB - 1MB)
- Performance-critical scenarios
- **Recommended for most use cases**

---

### 4. `FlashBertTokenizerParallel` - Multi-threaded BERT

Parallel processing for very large documents.

```csharp
var tokenizer = new FlashBertTokenizerParallel(
    vocabFile: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,
    tokenizeChineseChars: true,
    maxDegreeOfParallelism: Environment.ProcessorCount,  // Use all CPU cores
    chunkSize: 256 * 1024       // 256KB chunks
);

List<int> ids = tokenizer.Encode(veryLargeDocument);

// Don't forget to dispose
tokenizer.Dispose();
```

**Configuration:**
- `maxDegreeOfParallelism`: Number of threads (default: CPU cores)
- `chunkSize`: Size of text chunks in bytes (default: 256KB)

**Use cases:**
- Very large files (> 1MB)
- Multi-core systems
- Batch processing

---

### 5. `AsyncTokenizerPipeline` - Async File Processing

Asynchronous streaming tokenization for files.

```csharp
using var pipeline = new AsyncTokenizerPipeline(
    vocabFile: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,
    tokenizeChineseChars: true,
    maxDegreeOfParallelism: Environment.ProcessorCount,
    chunkSize: 128 * 1024,      // 128KB chunks
    bufferSize: 1024 * 1024     // 1MB buffer
);

// Process file directly
List<int> ids = await pipeline.ProcessFileAsync("large_file.txt");

// Process text asynchronously
List<int> ids2 = await pipeline.ProcessTextAsync(largeText);
```

**Use cases:**
- File processing
- Async/await patterns
- Streaming scenarios
- Memory-efficient processing

---

### 6. `FlashBertTokenizerBidirectional` - Robust Fallback

Uses bidirectional heuristic for improved quality.

```csharp
var tokenizer = new FlashBertTokenizerBidirectional(
    vocabFile: "vocab.txt",
    doLowerCase: true,
    modelMaxLength: -1,
    tokenizeChineseChars: true
);

List<int> ids = tokenizer.Encode(
    text: complexText,
    padding: "longest",
    maxLength: -1
);
```

**How it works:**
- Tokenizes text both forward and backward
- Compares results using heuristics
- Selects the better tokenization
- Slightly slower but more robust

**Use cases:**
- Complex or ambiguous text
- Quality-critical applications
- When standard tokenization produces poor results

---

### 7. `BpeTokenizer` - GPT-2 Style BPE

Byte Pair Encoding for GPT-2 style models.

```csharp
var tokenizer = new BpeTokenizer(
    vocabJsonPath: "vocab.json",
    mergesPath: "merges.txt"
);

List<int> ids = tokenizer.Encode("The quick brown fox jumps over the lazy dog");
string text = tokenizer.Decode(ids);
```

**Use cases:**
- GPT-2, GPT-3 style models
- BPE-based tokenization
- Non-BERT models

---

### 8. `FlashTokenizer` - Unified Facade

High-level facade that auto-selects the appropriate tokenizer.

```csharp
// BERT WordPiece
var bertTokenizer = new FlashTokenizer(new TokenizerOptions
{
    VocabPath = "vocab.txt",
    DoLowerCase = true,
    ModelMaxLength = -1,        // Unlimited
    EnableBidirectional = false,
    Type = TokenizerType.Bert
});

// BPE
var bpeTokenizer = new FlashTokenizer(new TokenizerOptions
{
    Type = TokenizerType.BPE,
    BpeVocabJsonPath = "vocab.json",
    BpeMergesPath = "merges.txt"
});

// Enable bidirectional fallback
var robustTokenizer = new FlashTokenizer(new TokenizerOptions
{
    VocabPath = "vocab.txt",
    DoLowerCase = true,
    ModelMaxLength = -1,
    EnableBidirectional = true,  // More robust
    Type = TokenizerType.Bert
});
```

## Performance Guidelines

### Choosing the Right Tokenizer

| Text Size | Recommended Class | Reason |
|-----------|------------------|---------|
| < 1KB | `Tokenizer`, `FlashBertTokenizer` | Simple, low overhead |
| 1KB - 100KB | `FlashBertTokenizerOptimized` | Best single-thread performance |
| 100KB - 10MB | `FlashBertTokenizerParallel` | Multi-threading helps |
| > 10MB | `AsyncTokenizerPipeline` | Memory-efficient streaming |
| Any size + quality | `FlashBertTokenizerBidirectional` | Most robust |

### Performance Best Practices

#### 1. **Use Unlimited Length for Large Documents**
```csharp
// Good - unlimited length
var tokenizer = new FlashBertTokenizerOptimized("vocab.txt", true, -1);
var ids = tokenizer.Encode(text, "longest", -1);

// Bad - causes early stopping
var tokenizer = new FlashBertTokenizerOptimized("vocab.txt", true, 512);
var ids = tokenizer.Encode(text);  // Stops at 512 tokens
```

#### 2. **Proper Padding for Your Use Case**
```csharp
// For large documents (no padding needed)
var ids = tokenizer.Encode(text, "longest", -1);

// For fixed-size batches
var ids = tokenizer.Encode(text, "max_length", 512);
```

#### 3. **Warmup for Benchmarking**
```csharp
// Warmup JIT and GC
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
var warmup = tokenizer.Encode("warmup text");

// Actual measurement
var sw = Stopwatch.StartNew();
var ids = tokenizer.Encode(actualText);
sw.Stop();
```

#### 4. **Parallel Processing Configuration**
```csharp
var tokenizer = new FlashBertTokenizerParallel(
    "vocab.txt", true, -1, true,
    Environment.ProcessorCount,  // Match CPU cores
    256 * 1024                  // Tune chunk size for your data
);
```

#### 5. **Memory Management**
```csharp
// Dispose parallel tokenizers
using var tokenizer = new FlashBertTokenizerParallel(...);

// Or manually
var tokenizer = new FlashBertTokenizerParallel(...);
try 
{
    var ids = tokenizer.Encode(text);
}
finally 
{
    tokenizer.Dispose();
}
```

## Common Usage Patterns

### Pattern 1: Simple Application
```csharp
using FlashTokenizer;

class SimpleApp
{
    private static readonly FlashBertTokenizerOptimized _tokenizer = 
        new("vocab.txt", true, -1);
    
    public List<int> TokenizeText(string text)
    {
        return _tokenizer.Encode(text, "longest", -1);
    }
}
```

### Pattern 2: Batch Processing
```csharp
public async Task<List<List<int>>> ProcessFiles(string[] filePaths)
{
    using var pipeline = new AsyncTokenizerPipeline(
        "vocab.txt", true, -1, true,
        Environment.ProcessorCount, 128 * 1024, 1024 * 1024);
    
    var results = new List<List<int>>();
    foreach (var filePath in filePaths)
    {
        var ids = await pipeline.ProcessFileAsync(filePath);
        results.Add(ids);
    }
    return results;
}
```

### Pattern 3: Configuration-Driven
```csharp
public class TokenizerFactory
{
    public static ITokenizer Create(string configType, string vocabPath)
    {
        return configType.ToLower() switch
        {
            "fast" => new FlashBertTokenizerOptimized(vocabPath, true, -1),
            "parallel" => new FlashBertTokenizerParallel(vocabPath, true, -1, true, 
                Environment.ProcessorCount, 256 * 1024),
            "robust" => new FlashBertTokenizerBidirectional(vocabPath, true, -1),
            _ => new FlashBertTokenizer(vocabPath, true, -1)
        };
    }
}
```

### Pattern 4: Quality vs Performance Trade-off
```csharp
public List<int> TokenizeWithFallback(string text)
{
    // Try fast tokenizer first
    var fastTokenizer = new FlashBertTokenizerOptimized("vocab.txt", true, -1);
    var ids = fastTokenizer.Encode(text, "longest", -1);
    
    // If result seems poor, use bidirectional
    if (ShouldUseBidirectional(text, ids))
    {
        var robustTokenizer = new FlashBertTokenizerBidirectional("vocab.txt", true, -1);
        ids = robustTokenizer.Encode(text, "longest", -1);
    }
    
    return ids;
}
```

## Troubleshooting

### Common Issues

#### 1. **Incomplete Tokenization**
```csharp
// Problem: Early stopping due to max length
var ids = tokenizer.Encode(text);  // Uses default max length

// Solution: Explicit unlimited
var ids = tokenizer.Encode(text, "longest", -1);
```

#### 2. **Memory Issues**
```csharp
// Problem: Not disposing parallel tokenizers
var tokenizer = new FlashBertTokenizerParallel(...);
// Memory leak!

// Solution: Use using statement
using var tokenizer = new FlashBertTokenizerParallel(...);
```

#### 3. **Circular Dependency (NuGet)**
```
Error NU1108: Cycle detected
FlashTokenizer -> FlashTokenizer (>= 1.0.1)
```

**Solution:** Rename your project to something other than "FlashTokenizer".

### Performance Comparison

Expected performance on a 4MB file (~759K tokens):

| Tokenizer | Time | Throughput | Memory | Use Case |
|-----------|------|------------|---------|----------|
| `FlashBertTokenizer` | ~200ms | ~3.8M tokens/sec | ~500MB | Standard |
| `FlashBertTokenizerOptimized` | ~110ms | ~6.9M tokens/sec | ~740MB | **Recommended** |
| `FlashBertTokenizerParallel` | ~60ms | ~12.7M tokens/sec | ~800MB | Large files |
| `AsyncTokenizerPipeline` | ~80ms | ~9.5M tokens/sec | ~600MB | File processing |
| `FlashBertTokenizerBidirectional` | ~150ms | ~5.1M tokens/sec | ~750MB | Quality-first |

*Results may vary based on hardware and text complexity.*

## Advanced Configuration

### Custom Chunk Sizes
```csharp
// For memory-constrained environments
var tokenizer = new FlashBertTokenizerParallel(
    "vocab.txt", true, -1, true,
    maxDegreeOfParallelism: 2,    // Fewer threads
    chunkSize: 64 * 1024         // Smaller chunks
);

// For high-memory systems
var tokenizer = new FlashBertTokenizerParallel(
    "vocab.txt", true, -1, true,
    maxDegreeOfParallelism: Environment.ProcessorCount * 2,
    chunkSize: 1024 * 1024       // 1MB chunks
);
```

### Custom Buffer Sizes
```csharp
using var pipeline = new AsyncTokenizerPipeline(
    "vocab.txt", true, -1, true,
    Environment.ProcessorCount,
    chunkSize: 256 * 1024,
    bufferSize: 4 * 1024 * 1024  // 4MB buffer for large files
);
```

## Integration Examples

### ASP.NET Core Service
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<ITokenizer>(provider =>
        new FlashBertTokenizerOptimized("vocab.txt", true, -1));
}

[ApiController]
public class TokenizerController : ControllerBase
{
    private readonly ITokenizer _tokenizer;
    
    public TokenizerController(ITokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }
    
    [HttpPost("tokenize")]
    public ActionResult<List<int>> Tokenize([FromBody] string text)
    {
        var ids = _tokenizer.Encode(text);
        return Ok(ids);
    }
}
```

### Console Application
```csharp
class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: app <vocab_path> <input_file>");
            return;
        }
        
        string vocabPath = args[0];
        string inputFile = args[1];
        
        using var pipeline = new AsyncTokenizerPipeline(
            vocabPath, true, -1, true,
            Environment.ProcessorCount, 128 * 1024, 1024 * 1024);
        
        var stopwatch = Stopwatch.StartNew();
        var ids = await pipeline.ProcessFileAsync(inputFile);
        stopwatch.Stop();
        
        Console.WriteLine($"Tokenized {ids.Count:N0} tokens in {stopwatch.ElapsedMilliseconds:F2}ms");
        Console.WriteLine($"Throughput: {ids.Count / stopwatch.Elapsed.TotalSeconds:F0} tokens/sec");
    }
}
```

### Documentation
- **GitHub Repository**: [FlashTokenizer on GitHub](https://github.com/mufeezahmad/flash-tokenizer)

### Getting Help
- **Issues**: Report bugs on GitHub Issues
- **Feature Requests**: Suggest improvements via GitHub Discussions  
- **Documentation**: Check this guide and README.md on Github
- **Community**: Join discussions in the repository

### Contributing
We welcome contributions! Please see our contributing guidelines in the repository.

### Performance Benchmarks
Real-world performance results:
- **Hardware**: Modern multi-core CPU
- **Test File**: 4MB document (~759K tokens)
- **Best Result**: 60ms processing time (12.7M tokens/sec)

## License

FlashTokenizer is released under the **MIT License**. See the LICENSE file in the repository for details.

## Changelog

### Version 1.0.2
- Initial NuGet release
- Complete BERT WordPiece implementation
- GPT-2 BPE support
- Parallel and async processing
- Comprehensive documentation
- Production-ready performance