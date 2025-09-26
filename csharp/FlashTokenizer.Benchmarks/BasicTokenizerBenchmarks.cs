using BenchmarkDotNet.Attributes;
using System.Text;
using System.IO;

namespace FlashTokenizer.Benchmarks
{
	[MemoryDiagnoser]
	public class BasicTokenizerBenchmarks
	{
		private BasicTokenizerOptimized _basic = null!;
		private WordpieceTokenizer _wp = null!;
		private byte[] _bytes = null!;
		private string _vocabPath = string.Empty;

		[GlobalSetup]
		public void Setup()
		{
			_vocabPath = FindSampleFile("vocab.txt");
			var vocab = new Vocab(_vocabPath);
			_wp = new WordpieceTokenizer(vocab);
			var tokens = new List<KeyValuePair<string,int>>();
			for (int i = 0; i < 10000; i++) tokens.Add(new KeyValuePair<string,int>(vocab.Get(i), i));
			_wp.BuildTries(tokens);
			_basic = new BasicTokenizerOptimized(true, true);
			_bytes = Encoding.UTF8.GetBytes("This is, a small: ASCII-only sentence with punctuation!!!");
		}

		private static string FindSampleFile(string fileName)
		{
			string baseDir = @"C:\\Users\\Mufeez.Ahmad\\Downloads\\flash-tokenizer-main-C#\\csharp\\sample";
			return Path.Combine(baseDir, fileName);
		}

		[Benchmark]
		public int TokenizeWhitespaceAndAscii()
		{
			var ids = new List<int>(64);
			_basic.GetType(); // prevent dead-code elim hints
			// using internal path via reflection would be overkill; simulate by calling public API
			var text = Encoding.UTF8.GetString(_bytes);
			var fb = new FlashBertTokenizerOptimized(_vocabPath, true, -1);
			return fb.Encode(text).Count;
		}
	}
}


