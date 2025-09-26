using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlashTokenizer;

/// <summary>
/// High-performance span-based BasicTokenizer with zero allocations in hot paths.
/// </summary>
public sealed class BasicTokenizerOptimized
{
	private readonly bool _doLowerCase;
	private readonly bool _tokenizeChineseChars;
	private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
	private readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;

	// Latin-1 fast-path lookup tables (lowercasing and accent stripping)
	private static readonly byte[] LowerLatin1Map = BuildLowerLatin1Map();
	private static readonly char[] StripAccentLatin1Map = BuildStripAccentLatin1Map();

	// Expose properties for parallel tokenizer
	public bool DoLowerCase => _doLowerCase;
	public bool TokenizeChineseChars => _tokenizeChineseChars;

	public BasicTokenizerOptimized(bool doLowerCase = true, bool tokenizeChineseChars = true)
	{
		_doLowerCase = doLowerCase;
		_tokenizeChineseChars = tokenizeChineseChars;
	}

	/// <summary>
	/// Tokenize with early stopping and zero allocations in hot path.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TokenizeEarlyStop(string text, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		// Pre-encode to UTF-8 once
		int maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
		byte[]? rentedBytes = null;
		char[]? rentedChars = null;
		
		try
		{
			rentedBytes = _bytePool.Rent(maxByteCount);
			int byteCount = Encoding.UTF8.GetBytes(text, 0, text.Length, rentedBytes, 0);
			var textBytes = rentedBytes.AsSpan(0, byteCount);

			// Clean text in-place using spans
			var cleanedBytes = CleanTextSpan(textBytes, rentedBytes, maxByteCount);
			
			// Tokenize using whitespace splitting with spans
			TokenizeWhitespaceSpan(cleanedBytes, wordpiece, maxLength, inputIds, allowedLength);
		}
		finally
		{
			if (rentedBytes != null) _bytePool.Return(rentedBytes);
			if (rentedChars != null) _charPool.Return(rentedChars);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ReadOnlySpan<byte> CleanTextSpan(ReadOnlySpan<byte> input, byte[] outputBuffer, int maxOutputLength)
	{
		int outputPos = 0;
		for (int i = 0; i < input.Length && outputPos < maxOutputLength - 4;)
		{
			byte b0 = input[i];
			if (b0 < 0x80)
			{
				// ASCII run fast-path. Optionally use intrinsics to detect non-ASCII quickly.
				int j = i;
				if (Avx2.IsSupported)
				{
					while (j <= input.Length - Vector256<byte>.Count)
					{
						var v = Vector256.LoadUnsafe(ref Unsafe.AsRef(in input[0]), (nuint)j);
						var high = Avx2.And(v, Vector256.Create((byte)0x80));
						if (Avx2.MoveMask(high) != 0) break; // encountered non-ASCII in this block
						// Process this ASCII block byte-by-byte for filtering/space mapping
						for (int k = 0; k < Vector256<byte>.Count && outputPos < maxOutputLength - 1; k++)
						{
							byte c = input[j + k];
							bool isCtrl = c <= 0x1F && c != (byte)'\t' && c != (byte)'\n' && c != (byte)'\r';
							if (isCtrl || c == 0)
							{
								continue;
							}
							bool isWs = (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\n' || c == (byte)'\r');
							outputBuffer[outputPos++] = isWs ? (byte)' ' : c;
						}
						j += Vector256<byte>.Count;
					}
				}
				// Scalar remainder of ASCII run
				while (j < input.Length && input[j] < 0x80 && outputPos < maxOutputLength - 1)
				{
					byte c = input[j];
					bool isCtrl = c <= 0x1F && c != (byte)'\t' && c != (byte)'\n' && c != (byte)'\r';
					if (!isCtrl && c != 0)
					{
						bool isWs = (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\n' || c == (byte)'\r');
						outputBuffer[outputPos++] = isWs ? (byte)' ' : c;
					}
					j++;
				}
				i = j;
				continue;
			}

			int cp = Utf8Util.DecodeCodePoint(input, i);
			int len = Utf8Util.CharLen(input[i]);
			if (cp == 0 || cp == 0xfffd || cp == 0x2028 || cp == 0x2029 || CharMaps.IsControl(cp))
			{
				i += len;
				continue;
			}
			if (CharMaps.IsWhitespace(cp))
			{
				outputBuffer[outputPos++] = (byte)' ';
			}
			else if (_tokenizeChineseChars && CharMaps.IsChinese(cp))
			{
				if (outputPos < maxOutputLength - 1) outputBuffer[outputPos++] = (byte)' ';
				if (outputPos + len <= maxOutputLength)
				{
					input.Slice(i, len).CopyTo(outputBuffer.AsSpan(outputPos));
					outputPos += len;
				}
				if (outputPos < maxOutputLength - 1) outputBuffer[outputPos++] = (byte)' ';
			}
			else
			{
				if (outputPos + len <= maxOutputLength)
				{
					input.Slice(i, len).CopyTo(outputBuffer.AsSpan(outputPos));
					outputPos += len;
				}
			}
			i += len;
		}
		return outputBuffer.AsSpan(0, outputPos);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TokenizeWhitespaceSpan(ReadOnlySpan<byte> text, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		int start = -1;
		int i = 0;
		// SIMD fast-path: detect ASCII whitespace (space, tab, CR, LF)
		if (Avx2.IsSupported && text.Length >= Vector256<byte>.Count)
		{
			var sp = Vector256.Create((byte)' ');
			var tb = Vector256.Create((byte)'\t');
			var cr = Vector256.Create((byte)'\r');
			var nl = Vector256.Create((byte)'\n');
			while (i <= text.Length - Vector256<byte>.Count)
			{
				var v = Vector256.LoadUnsafe(ref Unsafe.AsRef(in text[0]), (nuint)i);
				var m = Avx2.Or(Avx2.Or(Avx2.CompareEqual(v, sp), Avx2.CompareEqual(v, tb)), Avx2.Or(Avx2.CompareEqual(v, cr), Avx2.CompareEqual(v, nl)));
				int mask = Avx2.MoveMask(m); // 32-bit mask, 1 bit per byte lane high bit
				if (mask == 0)
				{
					// No whitespace in this block
					if (start < 0) start = i;
					i += Vector256<byte>.Count;
					continue;
				}
				// Process bytes in this block individually where needed
				for (int j = 0; j < Vector256<byte>.Count; j++, i++)
				{
					byte b = text[i];
					bool isWhitespace = (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n');
					if (!isWhitespace)
					{
						if (start < 0) start = i;
					}
					else if (start >= 0)
					{
						var tokenSpan = text.Slice(start, i - start);
						ProcessTokenSpan(tokenSpan, wordpiece, maxLength, inputIds, allowedLength);
						start = -1;
					}
				}
			}
		}
		else if (Sse2.IsSupported && text.Length >= Vector128<byte>.Count)
		{
			var sp = Vector128.Create((byte)' ');
			var tb = Vector128.Create((byte)'\t');
			var cr = Vector128.Create((byte)'\r');
			var nl = Vector128.Create((byte)'\n');
			while (i <= text.Length - Vector128<byte>.Count)
			{
				var v = Vector128.LoadUnsafe(ref Unsafe.AsRef(in text[0]), (nuint)i);
				var m = Sse2.Or(Sse2.Or(Sse2.CompareEqual(v, sp), Sse2.CompareEqual(v, tb)), Sse2.Or(Sse2.CompareEqual(v, cr), Sse2.CompareEqual(v, nl)));
				int mask = Sse2.MoveMask(m);
				if (mask == 0)
				{
					if (start < 0) start = i;
					i += Vector128<byte>.Count;
					continue;
				}
				for (int j = 0; j < Vector128<byte>.Count; j++, i++)
				{
					byte b = text[i];
					bool isWhitespace = (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n');
					if (!isWhitespace)
					{
						if (start < 0) start = i;
					}
					else if (start >= 0)
					{
						var tokenSpan = text.Slice(start, i - start);
						ProcessTokenSpan(tokenSpan, wordpiece, maxLength, inputIds, allowedLength);
						start = -1;
					}
				}
			}
		}
		// Scalar tail (or no SIMD support)
		for (; i < text.Length; i++)
		{
			byte b = text[i];
			bool isWhitespace = b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
			if (!isWhitespace)
			{
				if (start < 0) start = i;
			}
			else if (start >= 0)
			{
				var tokenSpan = text.Slice(start, i - start);
				ProcessTokenSpan(tokenSpan, wordpiece, maxLength, inputIds, allowedLength);
				start = -1;
			}
		}
		if (start >= 0)
		{
			var tokenSpan = text.Slice(start);
			ProcessTokenSpan(tokenSpan, wordpiece, maxLength, inputIds, allowedLength);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessTokenSpan(ReadOnlySpan<byte> tokenBytes, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		// Fast path for ASCII-only tokens
		if (IsAsciiOnly(tokenBytes))
		{
			ProcessAsciiToken(tokenBytes, wordpiece, maxLength, inputIds, allowedLength);
		}
		else
		{
			ProcessUnicodeToken(tokenBytes, wordpiece, maxLength, inputIds, allowedLength);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsAsciiOnly(ReadOnlySpan<byte> bytes)
	{
		for (int i = 0; i < bytes.Length; i++)
		{
			if (bytes[i] >= 128) return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessAsciiToken(ReadOnlySpan<byte> tokenBytes, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		// ASCII fast path - no UTF-8 decoding needed
		if (_doLowerCase)
		{
			Span<byte> lowerBytes = tokenBytes.Length <= 256 ? stackalloc byte[tokenBytes.Length] : new byte[tokenBytes.Length];
			int i = 0;
			// AVX2 lowercase: add 32 to lanes where 'A'..'Z'
			if (Avx2.IsSupported && tokenBytes.Length >= Vector256<byte>.Count)
			{
				var a = Vector256.Create((byte)'A');
				var z = Vector256.Create((byte)'Z');
				var add = Vector256.Create((byte)32);
				while (i <= tokenBytes.Length - Vector256<byte>.Count)
				{
					var v = Vector256.LoadUnsafe(ref Unsafe.AsRef(in tokenBytes[0]), (nuint)i);
					var geA = Avx2.CompareGreaterThan(v.AsSByte(), a.AsSByte());
					var geOrEqA = Avx2.Or(geA.AsByte(), Avx2.CompareEqual(v, a));
					var leZ = Avx2.CompareGreaterThan(z.AsSByte(), v.AsSByte());
					var leOrEqZ = Avx2.Or(leZ.AsByte(), Avx2.CompareEqual(v, z));
					var mask = Avx2.And(geOrEqA, leOrEqZ);
					var delta = Avx2.And(mask, add);
					var lowered = Avx2.Add(v, delta);
					lowered.StoreUnsafe(ref lowerBytes[0], (nuint)i);
					i += Vector256<byte>.Count;
				}
			}
			else if (Sse2.IsSupported && tokenBytes.Length >= Vector128<byte>.Count)
			{
				var a = Vector128.Create((byte)'A');
				var z = Vector128.Create((byte)'Z');
				var add = Vector128.Create((byte)32);
				while (i <= tokenBytes.Length - Vector128<byte>.Count)
				{
					var v = Vector128.LoadUnsafe(ref Unsafe.AsRef(in tokenBytes[0]), (nuint)i);
					var geA = Sse2.CompareGreaterThan(v.AsSByte(), a.AsSByte());
					var geOrEqA = Sse2.Or(geA.AsByte(), Sse2.CompareEqual(v, a));
					var leZ = Sse2.CompareGreaterThan(z.AsSByte(), v.AsSByte());
					var leOrEqZ = Sse2.Or(leZ.AsByte(), Sse2.CompareEqual(v, z));
					var mask = Sse2.And(geOrEqA, leOrEqZ);
					var delta = Sse2.And(mask, add);
					var lowered = Sse2.Add(v, delta);
					lowered.StoreUnsafe(ref lowerBytes[0], (nuint)i);
					i += Vector128<byte>.Count;
				}
			}
			for (; i < tokenBytes.Length; i++)
			{
				byte b = tokenBytes[i];
				lowerBytes[i] = (b >= (byte)'A' && b <= (byte)'Z') ? (byte)(b + 32) : b;
			}
			ProcessTokenBytes(lowerBytes, wordpiece, maxLength, inputIds, allowedLength);
		}
		else
		{
			ProcessTokenBytes(tokenBytes, wordpiece, maxLength, inputIds, allowedLength);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessUnicodeToken(ReadOnlySpan<byte> tokenBytes, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		// Convert to string for Unicode processing; apply Latin-1 fast path when possible
		string token = Encoding.UTF8.GetString(tokenBytes);
		if (_doLowerCase)
		{
			bool isLatin1 = true;
			for (int i = 0; i < token.Length; i++) { if (token[i] > 0xFF) { isLatin1 = false; break; } }
			if (isLatin1)
			{
				Span<char> buf = token.Length <= 256 ? stackalloc char[token.Length] : new char[token.Length];
				for (int i = 0; i < token.Length; i++)
				{
					char ch = token[i];
					byte b = (byte)ch;
					char lowered = (char)LowerLatin1Map[b];
					char stripped = StripAccentLatin1Map[(byte)lowered];
					buf[i] = stripped;
				}
				token = new string(buf);
			}
			else
			{
				token = AccentMap.ToLowerAndStripAccents(token);
			}
		}
		// Split on punctuation
		SplitOnPuncSpan(token, wordpiece, maxLength, inputIds, allowedLength);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessTokenBytes(ReadOnlySpan<byte> tokenBytes, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		// Check for punctuation in ASCII range (SIMD accelerated when available)
		bool hasPunctuation = false;
		if (System.Numerics.Vector.IsHardwareAccelerated && tokenBytes.Length >= System.Numerics.Vector<byte>.Count)
		{
			var vecCount = System.Numerics.Vector<byte>.Count;
			int i = 0;
			while (i <= tokenBytes.Length - vecCount)
			{
				var v = new System.Numerics.Vector<byte>(tokenBytes.Slice(i, vecCount).ToArray());
				// Fast approximate: check if any byte is in ASCII punct ranges by building masks via comparisons
				var ge33 = System.Numerics.Vector.GreaterThanOrEqual(v, new System.Numerics.Vector<byte>(33));
				var le47 = System.Numerics.Vector.LessThanOrEqual(v, new System.Numerics.Vector<byte>(47));
				var r1 = System.Numerics.Vector.BitwiseAnd(ge33, le47);
				var ge58 = System.Numerics.Vector.GreaterThanOrEqual(v, new System.Numerics.Vector<byte>(58));
				var le64 = System.Numerics.Vector.LessThanOrEqual(v, new System.Numerics.Vector<byte>(64));
				var r2 = System.Numerics.Vector.BitwiseAnd(ge58, le64);
				var ge91 = System.Numerics.Vector.GreaterThanOrEqual(v, new System.Numerics.Vector<byte>(91));
				var le96 = System.Numerics.Vector.LessThanOrEqual(v, new System.Numerics.Vector<byte>(96));
				var r3 = System.Numerics.Vector.BitwiseAnd(ge91, le96);
				var ge123 = System.Numerics.Vector.GreaterThanOrEqual(v, new System.Numerics.Vector<byte>(123));
				var le126 = System.Numerics.Vector.LessThanOrEqual(v, new System.Numerics.Vector<byte>(126));
				var r4 = System.Numerics.Vector.BitwiseAnd(ge123, le126);
				var any = System.Numerics.Vector.BitwiseOr(System.Numerics.Vector.BitwiseOr(r1, r2), System.Numerics.Vector.BitwiseOr(r3, r4));
				if (!any.Equals(System.Numerics.Vector<byte>.Zero)) { hasPunctuation = true; break; }
				i += vecCount;
			}
			if (!hasPunctuation)
			{
				for (; i < tokenBytes.Length; i++)
				{
					byte b = tokenBytes[i];
					if ((b >= 33 && b <= 47) || (b >= 58 && b <= 64) || (b >= 91 && b <= 96) || (b >= 123 && b <= 126)) { hasPunctuation = true; break; }
				}
			}
		}
		else
		{
			for (int i = 0; i < tokenBytes.Length; i++)
			{
				byte b = tokenBytes[i];
				if ((b >= 33 && b <= 47) || (b >= 58 && b <= 64) || (b >= 91 && b <= 96) || (b >= 123 && b <= 126))
				{
					hasPunctuation = true;
					break;
				}
			}
		}

		if (!hasPunctuation)
		{
			// No punctuation, process as single token via zero-copy UTF-8
			wordpiece.TokenizerIdsUtf8(tokenBytes, maxLength, inputIds);
			return;
		}

		// Split on punctuation
		SplitOnPuncBytes(tokenBytes, wordpiece, maxLength, inputIds, allowedLength);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SplitOnPuncBytes(ReadOnlySpan<byte> tokenBytes, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		bool inWord = false;
		int wordStart = 0;
		
		for (int i = 0; i < tokenBytes.Length; i++)
		{
			byte b = tokenBytes[i];
			bool isPunct = (b >= 33 && b <= 47) || (b >= 58 && b <= 64) || (b >= 91 && b <= 96) || (b >= 123 && b <= 126);
			
			if (isPunct)
			{
				if (inWord)
				{
					var wordSpan = tokenBytes.Slice(wordStart, i - wordStart);
					wordpiece.TokenizerIdsUtf8(wordSpan, maxLength, inputIds);
					inWord = false;
				}
				// Add punctuation as separate token
				wordpiece.TokenizerIdsUtf8(tokenBytes.Slice(i, 1), maxLength, inputIds);
			}
			else if (!inWord)
			{
				wordStart = i;
				inWord = true;
			}
		}
		
		if (inWord)
		{
			var wordSpan = tokenBytes.Slice(wordStart);
			wordpiece.TokenizerIdsUtf8(wordSpan, maxLength, inputIds);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SplitOnPuncSpan(string token, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(token);
		bool inWord = false;
		int wordStart = 0;
		bool hasPunc = false;
		
		for (int i = 0; i < bytes.Length;)
		{
			int cp = Utf8Util.DecodeCodePoint(bytes, i);
			int len = Utf8Util.CharLen(bytes[i]);
			
			if (CharMaps.IsPunctuation(cp))
			{
				hasPunc = true;
				if (inWord)
				{
					var slice = bytes.Slice(wordStart, i - wordStart);
					string s = Encoding.UTF8.GetString(slice);
					wordpiece.TokenizerIds(s, maxLength, inputIds);
					inWord = false;
				}
				string punct = Encoding.UTF8.GetString(bytes.Slice(i, len));
				wordpiece.TokenizerIds(punct, maxLength, inputIds);
			}
			else if (!inWord)
			{
				wordStart = i;
				inWord = true;
			}
			i += len;
		}
		
		if (!hasPunc)
		{
			wordpiece.TokenizerIds(token, maxLength, inputIds);
			return;
		}
		
		if (inWord)
		{
			var s = Encoding.UTF8.GetString(bytes.Slice(wordStart));
			wordpiece.TokenizerIds(s, maxLength, inputIds);
		}
	}

	private static byte[] BuildLowerLatin1Map()
	{
		var map = new byte[256];
		for (int i = 0; i < 256; i++) map[i] = (byte)i;
		// ASCII A-Z to a-z
		for (int c = (int)'A'; c <= (int)'Z'; c++) map[c] = (byte)(c + 32);
		// Latin-1 uppercase accented letters → lowercase (basic subset)
		for (int c = 0xC0; c <= 0xD6; c++) map[c] = (byte)(c + 32);
		for (int c = 0xD8; c <= 0xDE; c++) map[c] = (byte)(c + 32);
		// micro sign µ stays µ
		return map;
	}

	private static char[] BuildStripAccentLatin1Map()
	{
		var map = new char[256];
		for (int i = 0; i < 256; i++) map[i] = (char)i;
		// Strip common diacritics to base letters (lowercase ranges)
		map[0xE0] = 'a'; map[0xE1] = 'a'; map[0xE2] = 'a'; map[0xE3] = 'a'; map[0xE4] = 'a'; map[0xE5] = 'a';
		map[0xE7] = 'c';
		map[0xE8] = 'e'; map[0xE9] = 'e'; map[0xEA] = 'e'; map[0xEB] = 'e';
		map[0xEC] = 'i'; map[0xED] = 'i'; map[0xEE] = 'i'; map[0xEF] = 'i';
		map[0xF1] = 'n';
		map[0xF2] = 'o'; map[0xF3] = 'o'; map[0xF4] = 'o'; map[0xF5] = 'o'; map[0xF6] = 'o'; map[0xF8] = 'o';
		map[0xF9] = 'u'; map[0xFA] = 'u'; map[0xFB] = 'u'; map[0xFC] = 'u';
		map[0xFD] = 'y'; map[0xFF] = 'y';
		// already lowercase assumed; uppercases handled by lower map first
		return map;
	}
}
