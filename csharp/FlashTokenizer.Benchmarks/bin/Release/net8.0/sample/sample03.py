from flash_tokenizer import BertTokenizerFlash

texts = [
    "Chess is Life.",
    "Dies Spiel ist ein Probierstein des Gehirns.",
    "Chess is the game which reflects most honour on human wit.",
    "Для меня шахматы не игра, а искусство.",
    "Chess constitutes an effective means in order to education and training of the intellect of man.",
    "Шахматы - это искусство анализа.",
    "Шахматы – и спорт, и наука, и искусство.",
    "Chess doesn’t drive people mad, it keeps mad people sane.",
    "Chess is as much a mystery as women.",
    "Chess is a fairy tale of a thousand and one blunders."
]
tokenizer = BertTokenizerFlash.from_pretrained('bert-base-multilingual-uncased'
                                               , original=True)
# If you put True in original, you can call verify with the first Python implementation.

correct = 0
for text in texts:
    same, ids_flash, ids_original = tokenizer.verify(text)
    correct += same

print(f'Accuracy: {correct * 100.0 / len(texts):.2f}%')
