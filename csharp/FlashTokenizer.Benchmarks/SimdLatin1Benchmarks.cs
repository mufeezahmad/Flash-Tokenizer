using BenchmarkDotNet.Attributes;
using System.Text;
using System.IO;

namespace FlashTokenizer.Benchmarks
{
	[MemoryDiagnoser]
	public class SimdLatin1Benchmarks
	{
		private BasicTokenizerOptimized _basic = null!;
		private WordpieceTokenizer _wp = null!;
		private string _latin1Text = "Café naïve façade coöperate résumé überstraße";

		[GlobalSetup]
		public void Setup()
		{
			var vocabPath = FindSampleFile("vocab.txt");
			var vocab = new Vocab(vocabPath);
			_wp = new WordpieceTokenizer(vocab);
			var tokens = new List<KeyValuePair<string,int>>();
			for (int i = 0; i < 10000; i++) tokens.Add(new KeyValuePair<string,int>(vocab.Get(i), i));
			_wp.BuildTries(tokens);
			_basic = new BasicTokenizerOptimized(true, true);
		}

		[Benchmark]
		public int Latin1LowerStrip()
		{
			var ids = new List<int>(128);
			_basic.TokenizeEarlyStop(_latin1Text, _wp, int.MaxValue, ids, int.MaxValue - 1);
			return ids.Count;
		}

		private static string FindSampleFile(string fileName)
		{
			string baseDir = @"C:\\Users\\Mufeez.Ahmad\\Downloads\\flash-tokenizer-main-C#\\csharp\\sample";
			return Path.Combine(baseDir, fileName);
		}
	}
}


