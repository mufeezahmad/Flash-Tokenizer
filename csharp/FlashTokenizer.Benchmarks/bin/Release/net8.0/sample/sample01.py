from flash_tokenizer import BertTokenizerFlash
from transformers import BertTokenizer

titles = [
    '绝不能放弃，世界上没有失败，只有放弃。',
    'is there any doubt about it "None whatsoever"',
    "세상 어떤 짐승이 이를 드러내고 사냥을 해? 약한 짐승이나 몸을 부풀리지, 진짜 짐승은 누구보다 침착하지.",
    'そのように二番目に死を偽装して生き残るようになったイタドリがどうして初めて見る自分をこんなに気遣ってくれるのかと尋ねると「私が大切にする人たちがあなたを大切にするから」と答えては'
]

vocab_file = "vocab.txt"

tokenizer1 = BertTokenizerFlash(vocab_file, do_lower_case=False, model_max_length=512)
tokenizer2 = BertTokenizer(vocab_file, do_lower_case=False, model_max_length=512)

for title in titles:
    print(title)
    tokens1 = tokenizer1.tokenize(title)
    tokens2 = tokenizer2.tokenize(title)
    ids1 = tokenizer1(title, max_length=512, padding="longest").input_ids[0]
    ids2 = tokenizer2(title, max_length=512, padding="longest", return_tensors="np").input_ids[0].tolist()
    if tokens1 == tokens2 and ids1 == ids2:
        print("Accept!")
    else:
        print("Wrong Answer")
    print(tokens1)
    print(tokens2)
    print(ids1)
    print(ids2)
    print()
