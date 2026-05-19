from sentence_transformers import SentenceTransformer
model = SentenceTransformer('all-MiniLM-L6-v2')
model.save('./models/minilm')
print("Done — model saved to ./models/minilm")