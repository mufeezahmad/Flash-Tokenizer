using BenchmarkDotNet.Attributes;
using System.Text;

namespace FlashTokenizer.Benchmarks
{
	[MemoryDiagnoser]
	public class TrieBenchmarks
	{
		private ACTrie _trie = null!;
		private ReadOnlyMemory<byte> _token;

		[GlobalSetup]
		public void Setup()
		{
			_trie = new ACTrie();
			_trie.Insert("hello", 1);
			_trie.Insert("hell", 2);
			_trie.Insert("he", 3);
			_trie.Insert("token", 4);
			_trie.Build();
			_token = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("hellotoken"));
		}

		[Benchmark]
		public (int length, int index) Search()
		{
			return _trie.Search(_token.Span, 0);
		}
	}
}


