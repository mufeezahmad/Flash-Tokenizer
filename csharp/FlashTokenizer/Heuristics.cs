namespace FlashTokenizer;

internal static class Heuristics
{
	// Mirrors functions.h: compare_ids filtering and lexicographic compare of sorted >=4 ids
	public static bool CompareIds(List<int> a, List<int> b)
	{
		if (a.Count == 0) return true;
		if (b.Count == 0) return false;
		int minA = int.MaxValue, minB = int.MaxValue;
		for (int i = 0; i < a.Count; i++) if (a[i] < minA) minA = a[i];
		for (int i = 0; i < b.Count; i++) if (b[i] < minB) minB = b[i];
		if (minA < minB) return true;
		var fa = FilterAndSort(a);
		var fb = FilterAndSort(b);
		int n = Math.Min(fa.Count, fb.Count);
		for (int i = 0; i < n; i++)
		{
			if (fa[i] != fb[i]) return fa[i] < fb[i];
		}
		return fa.Count < fb.Count;
	}

	private static List<int> FilterAndSort(List<int> src)
	{
		var dst = new List<int>(src.Count);
		for (int i = 0; i < src.Count; i++) if (src[i] >= 4) dst.Add(src[i]);
		dst.Sort();
		return dst;
	}
}


