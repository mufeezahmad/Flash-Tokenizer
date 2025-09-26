namespace FlashTokenizer;

/// <summary>
/// Simple tokenizer abstraction to support multiple algorithms (e.g., WordPiece, BPE).
/// </summary>
public interface ITokenizer
{
	/// <summary>
	/// Encode input text to token ids.
	/// </summary>
	/// <param name="input">Text to tokenize.</param>
	/// <returns>List of token ids.</returns>
	List<int> Encode(string input);

	/// <summary>
	/// Decode token ids back to text.
/// </summary>
	/// <param name="ids">Token ids to decode.</param>
	/// <returns>Decoded text.</returns>
	string Decode(IEnumerable<int> ids);
}


