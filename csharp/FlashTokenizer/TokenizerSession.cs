using System.Buffers;

namespace FlashTokenizer;

public sealed class TokenizerSession : IDisposable
{
	private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
	private byte[]? _buffer;
	public List<int> InputIds { get; } = new List<int>(1024);

	public byte[] RentBuffer(int minSize)
	{
		if (_buffer == null || _buffer.Length < minSize)
		{
			if (_buffer != null) _bytePool.Return(_buffer);
			_buffer = _bytePool.Rent(minSize);
		}
		return _buffer;
	}

	public void Reset()
	{
		InputIds.Clear();
	}

	public void Dispose()
	{
		if (_buffer != null) { _bytePool.Return(_buffer); _buffer = null; }
	}
}


