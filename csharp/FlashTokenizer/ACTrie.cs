using System.Runtime.CompilerServices;

namespace FlashTokenizer;



public sealed class ACTrie
{
	private struct Node
	{
		public int Fail;
		public int VocabIndex;
		public int WordLen;
		public int[] Next;     // size 256
		public bool[] Explicit; // size 256
	}

	private List<Node> _pool = new();
	private int[] _dfa = Array.Empty<int>();
	private ulong[] _explicitBits = Array.Empty<ulong>(); // 4 ulongs per state (256 bits)
	private int[] _vocabIndex = Array.Empty<int>();

	public ACTrie()
	{
		var next = new int[256];
		var explicitFlags = new bool[256];
		Array.Fill(next, -1);
		_pool.Add(new Node { Fail = 0, VocabIndex = -1, WordLen = 0, Next = next, Explicit = explicitFlags });
	}

	public void Insert(string word, int index)
	{
		int node = 0;
		foreach (byte b in System.Text.Encoding.UTF8.GetBytes(word))
		{
			if (_pool[node].Next[b] == -1)
			{
				_pool[node].Next[b] = _pool.Count;
				_pool[node].Explicit[b] = true;
				var newNext = new int[256];
				var newExplicit = new bool[256];
				Array.Fill(newNext, -1);
				_pool.Add(new Node { Fail = 0, VocabIndex = -1, WordLen = 0, Next = newNext, Explicit = newExplicit });
			}
			node = _pool[node].Next[b];
		}
		var n = _pool[node];
		n.VocabIndex = index;
		n.WordLen = word.Length; // byte length is used during traversal
		_pool[node] = n;
	}

	public void Build()
	{
		var q = new Queue<int>();
		for (int c = 0; c < 256; c++)
		{
			int child = _pool[0].Next[c];
			if (child != -1)
			{
				var cn = _pool[child];
				cn.Fail = 0;
				_pool[child] = cn;
				q.Enqueue(child);
			}
			else
			{
				_pool[0].Next[c] = 0;
			}
		}
		while (q.Count > 0)
		{
			int cur = q.Dequeue();
			int f = _pool[cur].Fail;
			for (int c = 0; c < 256; c++)
			{
				int nxt = _pool[cur].Next[c];
				if (nxt != -1 && _pool[cur].Explicit[c])
				{
					var nn = _pool[nxt];
					nn.Fail = _pool[f].Next[c];
					_pool[nxt] = nn;
					q.Enqueue(nxt);
				}
				else
				{
					_pool[cur].Next[c] = _pool[f].Next[c];
				}
			}
		}
		int states = _pool.Count;
		_dfa = new int[states * 256];
		_explicitBits = new ulong[states * 4];
		_vocabIndex = new int[states];
		for (int s = 0; s < states; s++)
		{
			_vocabIndex[s] = _pool[s].VocabIndex;
			for (int c = 0; c < 256; c++)
			{
				int idx = s * 256 + c;
				_dfa[idx] = _pool[s].Next[c];
				if (_pool[s].Explicit[c])
				{
					int word = c >> 6; // 0..3
					int bit = c & 63;  // 0..63
					_explicitBits[s * 4 + word] |= 1UL << bit;
				}
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (int length, int index) Search(ReadOnlySpan<byte> token, int start)
	{
		int current = 0;
		int bestLen = 0;
		int bestIdx = -1;
		for (int pos = start; pos < token.Length; pos++)
		{
			byte c = token[pos];
			int idx = current * 256 + c;
			ulong bits = _explicitBits[current * 4 + (c >> 6)];
			if (((bits >> (c & 63)) & 1UL) == 0UL) break;
			int next = _dfa[idx];
			current = next;
			int vi = _vocabIndex[current];
			if (vi != -1)
			{
				bestLen = pos - start + 1;
				bestIdx = vi;
			}
		}
		return (bestLen, bestIdx);
	}
}


