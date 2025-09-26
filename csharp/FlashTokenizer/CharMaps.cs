using System.Runtime.CompilerServices;

namespace FlashTokenizer;

internal static class CharMaps
{
	// For parity, implement methods via explicit code point checks mirroring upstream ranges.

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsPunctuation(int cp)
	{
		// ASCII common punct ranges
		if ((cp >= 33 && cp <= 47) || (cp >= 58 && cp <= 64) || (cp >= 91 && cp <= 96) || (cp >= 123 && cp <= 126)) return true;
		// Unicode punctuation blocks mirroring upstream
		if ((cp >= 0x2000 && cp <= 0x206F) || (cp >= 0x3000 && cp <= 0x303F) || (cp >= 0xFF00 && cp <= 0xFFEF) || (cp >= 0xFE30 && cp <= 0xFE4F)) return true;
		// Selected special punctuation used upstream
		return cp is 0x201C or 0x201D or 0x2018 or 0x2019 or 0x300C or 0x300D or 0x300E or 0x300F or 0xFF5F or 0xFF60 or 0x2E80 or 0x2E99 or 0x2E9B or 0x2EF3 or 0x2028 or 0x2029 or 0x30FB or 183;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsControl(int cp)
	{
		if (cp <= 0x1F)
			return cp is not 0x09 and not 0x0A and not 0x0D; // allow tab, LF, CR
		if (cp >= 0x7F && cp <= 0x9F) return true;
		if (cp >= 0x200B && cp <= 0x200F) return true; // zero-width and directional marks
		if (cp >= 0x202A && cp <= 0x202E) return true; // embeddings/overrides
		if (cp >= 0x2060 && cp <= 0x2064) return true; // word joiners etc.
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsWhitespace(int cp)
	{
		// ASCII spaces
		if (cp == 0x20 || cp == 0x09 || cp == 0x0A || cp == 0x0D) return true;
		// Unicode space separators (including NBSP)
		return cp is 0x00A0 or 0x1680 or 0x2000 or 0x2001 or 0x2002 or 0x2003 or 0x2004 or 0x2005 or 0x2006 or 0x2007 or 0x2008 or 0x2009 or 0x200A or 0x202F or 0x205F or 0x3000;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsChinese(int cp)
	{
		if (cp is >= 0x4E00 and <= 0x9FFF) return true;
		if (cp is >= 0x3400 and <= 0x4DBF) return true;
		if (cp is >= 0xF900 and <= 0xFAFF) return true;
		if (cp is >= 0x20000 and <= 0x2A6DF) return true;
		if (cp is >= 0x2A700 and <= 0x2B73F) return true;
		if (cp is >= 0x2B740 and <= 0x2B81F) return true;
		if (cp is >= 0x2B820 and <= 0x2CEAF) return true;
		if (cp is >= 0x2F800 and <= 0x2FA1F) return true;
		return false;
	}
}


