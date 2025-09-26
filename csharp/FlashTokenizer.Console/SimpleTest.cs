using System;
using System.Diagnostics;
using System.IO;
using FlashTokenizer;

class SimpleTest
{
	static void Main()
	{
		try
		{
			Console.WriteLine("🧪 Simple Parallel Tokenizer Test");
			
			string filePath = @"C:\Users\Mufeez.Ahmad\Downloads\jazz_pakistan_faq_Copy.md";
			if (!File.Exists(filePath)) 
			{ 
				Console.WriteLine("File not found!"); 
				return; 
			}

			string vocabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt"));
			Console.WriteLine($"Vocab exists: {File.Exists(vocabPath)}");
			
			// Test with a smaller sample first
			string text = File.ReadAllText(filePath);
			string sampleText = text.Substring(0, Math.Min(500000, text.Length)); // 500KB sample
			Console.WriteLine($"Sample size: {sampleText.Length:N0} chars");

			// Test simple parallel tokenizer
			var tokenizer = new FlashBertTokenizerSimpleParallel(vocabPath, doLowerCase: true, modelMaxLength: -1, 
				tokenizeChineseChars: true, maxDegreeOfParallelism: 4, chunkSize: 128 * 1024);

			Console.WriteLine("Starting tokenization...");
			var sw = Stopwatch.StartNew();
			var ids = tokenizer.Encode(sampleText, padding: "longest", maxLength: -1);
			sw.Stop();

			Console.WriteLine($"✅ Success!");
			Console.WriteLine($"🧮 Tokens: {ids.Count:N0}");
			Console.WriteLine($"⏱️ Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
			Console.WriteLine($"📊 Throughput: {ids.Count / sw.Elapsed.TotalSeconds:F0} tokens/sec");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
			Console.WriteLine($"Stack: {ex.StackTrace}");
		}
	}
}
