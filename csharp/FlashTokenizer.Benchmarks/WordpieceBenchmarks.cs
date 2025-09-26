using BenchmarkDotNet.Attributes;
using FlashTokenizer;
using System.Text;
using System.IO;

namespace FlashTokenizer.Benchmarks
{
	[MemoryDiagnoser]
	public class WordpieceBenchmarks
	{
		private WordpieceTokenizer _wp = null!;
		private ReadOnlyMemory<byte> _asciiBytes;

		[GlobalSetup]
		public void Setup()
		{
			var vocabPath = FindSampleFile("vocab.txt");
			var vocab = new Vocab(vocabPath);
			_wp = new WordpieceTokenizer(vocab);
			var tokens = new List<KeyValuePair<string,int>>();
			for (int i = 0; i < 10000; i++)
			{
				var t = vocab.Get(i);
				tokens.Add(new KeyValuePair<string,int>(t, i));
			}
			_wp.BuildTries(tokens);
			_asciiBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("hello_world_tokenization_example"));
		}

		private static string FindSampleFile(string fileName)
		{
			string baseDir = @"C:\\Users\\Mufeez.Ahmad\\Downloads\\flash-tokenizer-main-C#\\csharp\\sample";
			return Path.Combine(baseDir, fileName);
		}

		[Benchmark]
		public int TokenizerIdsUtf8()
		{
			var list = new List<int>(32);
			return _wp.TokenizerIdsUtf8(_asciiBytes.Span, int.MaxValue, list);
		}

		[Benchmark]
		public int TokenizerIdsString()
		{
			var list = new List<int>(32);
			return _wp.TokenizerIds("hello_world_tokenization_example", int.MaxValue, list);
		}
	}
}


