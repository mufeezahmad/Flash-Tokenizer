using BenchmarkDotNet.Running;

namespace FlashTokenizer.Benchmarks
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			BenchmarkRunner.Run(new[]
			{
				typeof(WordpieceBenchmarks),
				typeof(BasicTokenizerBenchmarks),
				typeof(TrieBenchmarks),
				typeof(SimdLatin1Benchmarks)
			});
		}
	}
}


