from flash_tokenizer import BertTokenizerFlash
from transformers import BertTokenizer

titles = [
    '绝不能放弃，世界上没有失败，只有放弃。',
    'is there any doubt about it "None whatsoever"',
    "세상 어떤 짐승이 이를 드러내고 사냥을 해? 약한 짐승이나 몸을 부풀리지, 진짜 짐승은 누구보다 침착하지.",
    'そのように二番目に死を偽装して生き残るようになったイタドリがどうして初めて見る自分をこんなに気遣ってくれるのかと尋ねると「私が大切にする人たちがあなたを大切にするから」と答えては'
]

tokenizer1 = BertTokenizerFlash.from_pretrained('bert-base-multilingual-cased')
tokenizer2 = BertTokenizer.from_pretrained('bert-base-multilingual-cased')

correct = 0
for title in titles:
    print(title)
    tokens1 = tokenizer1.tokenize(title)
    tokens2 = tokenizer2.tokenize(title)
    ids1 = tokenizer1(title, max_length=512, padding="longest").input_ids[0]
    ids2 = tokenizer2(title, max_length=512, padding="longest", return_tensors="np").input_ids[0].tolist()
    if tokens1 == tokens2 and ids1 == ids2:
        correct += 1
        print("Accept!")
    else:
        print("Wrong Answer")
    print(ids1)
    print(ids2)
    print()

print(f'Accuracy: {correct * 100.0 / len(titles):.2f}%')
