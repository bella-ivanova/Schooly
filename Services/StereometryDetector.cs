namespace StudyAssistant.Services;

/// <summary>
/// Detects whether a student question is about 3D stereometry (solid geometry)
/// using a keyword heuristic covering both Bulgarian and English terms.
/// </summary>
public static class StereometryDetector
{
    private static readonly string[] Keywords =
    [
        // ── English ───────────────────────────────────────────────────────────
        "cube", "cuboid", "rectangular prism", "pyramid", "cone",
        "cylinder", "sphere", "tetrahedron", "prism", "parallelepiped",
        "diagonal", "cross-section", "cross section",
        "perpendicular plane", "perpendicular planes",
        "parallel plane", "parallel planes",
        "skew line", "skew lines",
        "dihedral angle", "dihedral",
        "stereometry", "solid geometry", "space geometry",
        "3d shape", "three-dimensional",
        "face", "edge", "vertex", "vertices",
        // ── Bulgarian ─────────────────────────────────────────────────────────
        "куб", "кубоид", "пирамида", "конус", "цилиндър",
        "сфера", "тетраедър", "призма", "паралелепипед",
        "равнина", "диагонал", "ръб", "връх", "върхове", "сечение",
        "перпендикулярни равнини", "успоредни равнини",
        "кръстосани прави", "двустенен ъгъл",
        "стереометрия", "правилна пирамида",
        "правилна призма", "пространствена геометрия",
        "пространствено тяло",
    ];

    public static bool IsStereometryQuestion(string question)
    {
        var lower = question.ToLowerInvariant();
        return Keywords.Any(kw => lower.Contains(kw));
    }
}
