namespace StudyAssistant.Services;

// POCOs for (de)serializing the STEM pipeline's CLI test-harness question set
// (System.Text.Json). See TestData/StemFixtures/*.json for real data. Mirrors the
// shape of RetrievalFixtureModels.cs's fixture convention.
public class StemTestFixture
{
    public List<StemTestQuestion> Questions { get; set; } = [];
}

public class StemTestQuestion
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public int Grade { get; set; }

    // "Math" | "Physics" | "Chemistry" | "None" — logged against the classifier's actual
    // output for eyeballing, not asserted (the harness reports mismatches, it doesn't fail).
    public string ExpectedSubject { get; set; } = "";
}
