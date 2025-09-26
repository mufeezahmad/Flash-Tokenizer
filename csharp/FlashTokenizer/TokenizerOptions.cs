namespace FlashTokenizer;

/// <summary>
/// Options for configuring the tokenizer behavior.
/// </summary>
public sealed class TokenizerOptions
{
	/// <summary>
	/// Path to vocab.txt (BERT WordPiece) or equivalent file.
	/// </summary>
	public string? VocabPath { get; init; }

	/// <summary>
	/// Whether to lowercase and strip accents (as in original BERT-uncased).
	/// </summary>
	public bool DoLowerCase { get; init; } = true;

	/// <summary>
	/// Maximum sequence length; -1 means unlimited.
	/// </summary>
	public int ModelMaxLength { get; init; } = 128;

	/// <summary>
	/// Use bidirectional WordPiece fallback heuristic.
	/// </summary>
	public bool EnableBidirectional { get; init; } = false;

	/// <summary>
	/// Select tokenizer algorithm. Default: Bert.
	/// </summary>
	public TokenizerType Type { get; init; } = TokenizerType.Bert;

	/// <summary>
	/// Path to BPE vocab.json (for GPT-2 style BPE).
	/// </summary>
	public string? BpeVocabJsonPath { get; init; }

	/// <summary>
	/// Path to BPE merges.txt (for GPT-2 style BPE).
	/// </summary>
	public string? BpeMergesPath { get; init; }
}


