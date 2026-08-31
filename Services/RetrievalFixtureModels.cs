namespace StudyAssistant.Services;

// POCOs for (de)serializing a standing retrieval-quality regression fixture
// (System.Text.Json). See TestData/RetrievalFixtures/*.json for real data.
// Expected chunks are identified by (sourceFile, page, matchText) rather than a raw
// Qdrant point id, because UpsertChunksAsync assigns a fresh GUID per point on every
// ingest — an id-pinned fixture would silently break on the next reingest-grade run.
public class RetrievalFixture
{
    public int FixtureVersion { get; set; }
    public string SourceCollection { get; set; } = "";
    public int Grade { get; set; }
    public string CreatedAt { get; set; } = "";
    public List<FixtureQuestion> Questions { get; set; } = [];
}

public class FixtureQuestion
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";

    // "single-hop" | "multi-hop" | "colloquial" | "formula-plain-language"
    public string Type { get; set; } = "";
    public List<string> Tags { get; set; } = [];

    // Governs how multiple ExpectedChunks combine into a pass/fail for this question — two
    // distinct uses share this one flag:
    //   true  = every expected chunk must land in top-3 (multi-hop: the chunks are genuinely
    //           different facts, both needed to answer the question — e.g. q19/q20 below).
    //   false = any one expected chunk landing in top-3 counts as a pass (single-concept
    //           alternates: the chunks are different but equally-correct answers to the same
    //           question — e.g. q16 below, where a prose chunk and a formula chunk on
    //           different pages both correctly answer it). Only add an alternate this way
    //           after confirming via diagnose-retrieval that the candidate is actually a
    //           correct answer, not just topically related and highly ranked.
    public bool RequireAll { get; set; } = true;

    public List<ExpectedChunk> ExpectedChunks { get; set; } = [];
    public string Notes { get; set; } = "";
}

public class ExpectedChunk
{
    public string SourceFile { get; set; } = "";
    public int Page { get; set; }

    // A distinctive, verbatim substring of the chunk's text used to re-resolve it against
    // Qdrant at run time. If this substring isn't found on the given page, the fixture is
    // stale relative to the current ingestion and the runner should fail loudly rather
    // than silently mis-score.
    public string MatchText { get; set; } = "";

    // Audit trail only — the point id observed when this fixture entry was authored.
    // Never trusted for lookups (ids churn on reingest).
    public string PointIdAtCapture { get; set; } = "";
    public string Note { get; set; } = "";
}
