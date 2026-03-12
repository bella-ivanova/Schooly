using StudyAssistant.Services;

namespace StudyAssistant.Services;

public class RAGService
{
    private readonly Dictionary<int, PersistentVectorStore> _gradeStores = new();
    private readonly EmbeddingService _embeddingService;
    private readonly OllamaChatService _chat;

    // In-memory store for temporary PDFs uploaded during current chat
    private readonly List<(string Text, float[] Embedding, string Subject)> _temporaryChunks = new();

    private int _currentGrade = 0;

    public RAGService(OllamaChatService chat, EmbeddingService embeddingService)
    {
        _chat = chat;
        _embeddingService = embeddingService;
    }

    // Load grade knowledge from Database/DataJson/Grade{g}.json
    public void SetGrade(int grade)
    {
        _currentGrade = grade;

        for (int g = 1; g <= grade; g++)
        {
            if (!_gradeStores.ContainsKey(g))
            {
                var dbPath = Path.Combine("Database", "DataJson", $"Grade{g}.json");
                var store = new PersistentVectorStore(dbPath);

                // #4 — warn if the store was built with a different embedding model
                if (!string.IsNullOrWhiteSpace(store.EmbeddingModel) &&
                    store.EmbeddingModel != _embeddingService.Model)
                {
                    Console.WriteLine(
                        $"WARNING: Grade {g} knowledge base was ingested with '{store.EmbeddingModel}' " +
                        $"but current embedding model is '{_embeddingService.Model}'. " +
                        "Queries may return poor results. Re-ingest to fix.");
                }

                _gradeStores[g] = store;
            }
        }

        Console.WriteLine($"Grade set to {grade}. Knowledge loaded permanently.");
    }

    // Reads PDFs from Database/DataPdf/Grade{grade}/{Subject}/ subfolders.
    // Each subfolder name becomes the subject tag for its chunks.
    // Falls back to scanning the grade root for PDFs with no subject tag.
    public async Task IngestGradePDFsAsync(int grade)
    {
        var gradeFolder = Path.Combine("Database", "DataPdf", $"Grade{grade}");

        if (!Directory.Exists(gradeFolder))
        {
            Console.WriteLine($"No PDF folder found at {gradeFolder}");
            return;
        }

        var dbPath = Path.Combine("Database", "DataJson", $"Grade{grade}.json");
        Directory.CreateDirectory(Path.Combine("Database", "DataJson"));

        // Delete old data so re-ingestion always rebuilds from scratch
        if (File.Exists(dbPath))
            File.Delete(dbPath);
        else if (Directory.Exists(dbPath))
            Directory.Delete(dbPath, recursive: true);

        var store = new PersistentVectorStore(dbPath);
        store.SetEmbeddingModel(_embeddingService.Model); // #4 — save model name with store
        int totalIngested = 0;
        int skipped = 0;

        // Scan subject subfolders (e.g. Grade5/Math/, Grade5/Science/)
        var subjectFolders = Directory.GetDirectories(gradeFolder);
        foreach (var subjectFolder in subjectFolders)
        {
            var subject = Path.GetFileName(subjectFolder);
            var pdfFiles = Directory.GetFiles(subjectFolder, "*.pdf");

            foreach (var pdfPath in pdfFiles)
            {
                var fileKey = Path.Combine(subject, Path.GetFileName(pdfPath));

                if (store.IsFileIngested(fileKey))
                {
                    Console.WriteLine($"Skipping (already ingested): [{subject}] {Path.GetFileName(pdfPath)}");
                    skipped++;
                    continue;
                }

                Console.WriteLine($"Ingesting [{subject}] {Path.GetFileName(pdfPath)}...");
                var text = PDFLoader.LoadText(pdfPath);
                text = PDFLoader.CleanText(text);
                var chunks = PDFLoader.ChunkText(text);

                // #3 — catch embedding failure and print a clear actionable message
                List<float[]> embeddings;
                try
                {
                    embeddings = await _embeddingService.GetEmbeddingsAsync(chunks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nEmbedding failed: {ex.Message}");
                    Console.WriteLine($"Is '{_embeddingService.Model}' pulled? Run: ollama pull {_embeddingService.Model}");
                    Console.WriteLine($"Ingestion stopped. {totalIngested} file(s) were processed before the error.");
                    if (totalIngested > 0)
                    {
                        store.Save();
                        Console.WriteLine("Partial results saved.");
                    }
                    return;
                }

                for (int i = 0; i < chunks.Count; i++)
                    store.Add(chunks[i], embeddings[i], subject);

                store.AddIngestedFile(fileKey);
                totalIngested++;
                Console.WriteLine($"  → {chunks.Count} chunks");
            }
        }

        // Also scan PDFs directly in the grade root (no subject tag)
        var rootPdfs = Directory.GetFiles(gradeFolder, "*.pdf");
        foreach (var pdfPath in rootPdfs)
        {
            var fileKey = Path.GetFileName(pdfPath);

            if (store.IsFileIngested(fileKey))
            {
                Console.WriteLine($"Skipping (already ingested): {fileKey}");
                skipped++;
                continue;
            }

            Console.WriteLine($"Ingesting {fileKey}...");
            var text = PDFLoader.LoadText(pdfPath);
            text = PDFLoader.CleanText(text);
            var chunks = PDFLoader.ChunkText(text);

            // #3 — same failure handling for root PDFs
            List<float[]> embeddings;
            try
            {
                embeddings = await _embeddingService.GetEmbeddingsAsync(chunks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nEmbedding failed: {ex.Message}");
                Console.WriteLine($"Is '{_embeddingService.Model}' pulled? Run: ollama pull {_embeddingService.Model}");
                Console.WriteLine($"Ingestion stopped. {totalIngested} file(s) were processed before the error.");
                if (totalIngested > 0)
                {
                    store.Save();
                    Console.WriteLine("Partial results saved.");
                }
                return;
            }

            for (int i = 0; i < chunks.Count; i++)
                store.Add(chunks[i], embeddings[i]);

            store.AddIngestedFile(fileKey);
            totalIngested++;
            Console.WriteLine($"  → {chunks.Count} chunks");
        }

        if (totalIngested == 0)
        {
            Console.WriteLine($"No PDF files found in {gradeFolder}");
            return;
        }

        store.Save();
        var skipMsg = skipped > 0 ? $" ({skipped} skipped)" : "";
        Console.WriteLine($"Grade {grade} ingestion complete. {totalIngested} file(s) ingested{skipMsg} → {dbPath}");

        // Reload into active store if this grade is already loaded
        if (_gradeStores.ContainsKey(grade))
            _gradeStores[grade] = new PersistentVectorStore(dbPath);
    }

    // Adds a PDF temporarily for the current chat session only (not saved to disk)
    public async Task AddTemporaryPDFAsync(string pdfPath, string subject = "")
    {
        var text = PDFLoader.LoadText(pdfPath);
        text = PDFLoader.CleanText(text);
        var chunks = PDFLoader.ChunkText(text);
        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunks);

        for (int i = 0; i < chunks.Count; i++)
            _temporaryChunks.Add((chunks[i], embeddings[i], subject));

        var label = string.IsNullOrWhiteSpace(subject) ? "" : $" [{subject}]";
        Console.WriteLine($"Loaded '{Path.GetFileName(pdfPath)}'{label} temporarily — {chunks.Count} chunks.");
    }

    // #12 — clear temp PDFs (called by /clear in Program.cs)
    public void ClearTemporaryChunks() => _temporaryChunks.Clear();

    public async Task Ask(string question)
    {
        if (_currentGrade == 0 && _temporaryChunks.Count == 0)
        {
            await _chat.StreamMessageAsync(question);
            return;
        }

        var qEmbeddingList = await _embeddingService.GetEmbeddingsAsync(new List<string> { question });
        var queryEmbedding = qEmbeddingList.First();

        var combinedChunks = new List<(string Text, string Subject, int Grade)>();

        const float MinScore = 0.1f;

        // Search permanent grade stores
        for (int g = 1; g <= _currentGrade; g++)
        {
            if (_gradeStores.TryGetValue(g, out var store))
            {
                foreach (var chunk in store.Query(queryEmbedding, topK: 10, minScore: MinScore))
                    combinedChunks.Add((chunk.Text, chunk.Subject, g));
            }
        }

        // Search temporary chunks
        if (_temporaryChunks.Count > 0)
        {
            var tempResults = _temporaryChunks
                .Select(x => new { x.Text, x.Subject, Score = CosineSimilarity(queryEmbedding, x.Embedding) })
                .Where(x => x.Score >= MinScore)
                .OrderByDescending(x => x.Score)
                .Take(5)
                .Select(x => (x.Text, x.Subject, Grade: _currentGrade));

            combinedChunks.AddRange(tempResults);
        }

        // If no relevant chunks were found, answer directly without the RAG wrapper
        if (combinedChunks.Count == 0)
        {
            await _chat.StreamMessageAsync(question);
            return;
        }

        // Format each chunk with grade + subject label
        var formattedChunks = combinedChunks.Select(c =>
        {
            var label = $"[Grade {c.Grade}";
            if (!string.IsNullOrWhiteSpace(c.Subject)) label += $" / {c.Subject}";
            label += "]";
            return $"{label}\n{c.Text}";
        });

        var context = string.Join("\n\n", formattedChunks);

        var gradeLabel = _currentGrade > 0 ? $"Grade {_currentGrade}" : "the student's current grade";
        var ragPrompt =
            $"You are a school tutor. The student is in {gradeLabel}. The following material is from their textbooks (content may span multiple grade levels and may be in a different language — translate as needed).\n" +
            "Each excerpt is labeled with its grade and subject. A unit title in the textbook may differ from what the student calls the topic — for example, a unit titled 'Solving Triangles' covers trigonometry.\n" +
            "IMPORTANT: Base your answer strictly on what is present in the excerpts below. Do NOT say a topic is absent or belongs to a different grade unless no related content appears in the excerpts.\n" +
            "Your job is to identify which excerpt is relevant to the student's question, then apply that topic's definitions, formulas, and methods step by step.\n" +
            "The student's problem may be a new example — you do not need an identical solved example in the text. Use the method the textbook teaches and work through the student's numbers.\n" +
            "Only say 'This is not covered in your current grade.' if the subject is entirely absent from the material below.\n\n" +
            "--- Textbook Material ---\n" +
            context +
            "\n--- End of Material ---\n\n" +
            "Student question: " + question;

        // #10 — pass original question as the history message, RAG prompt as the API message.
        // Only the clean question gets stored in conversation history.
        await _chat.StreamMessageAsync(question, ragPrompt);
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
