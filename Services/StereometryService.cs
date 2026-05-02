using System.Text.RegularExpressions;

namespace StudyAssistant.Services;

public static class StereometryService
{
    // ── Instruction appended to the RAG prompt for stereometry questions ──
    public const string Instruction =
        "At the end of your response, append a JSON block wrapped in <STEREO> and </STEREO> tags " +
        "describing the 3D geometry. Do not explain the JSON.\n" +
        "Format:\n" +
        "{\n" +
        "  \"vertices\": { \"A\":[x,y,z], \"B\":[x,y,z], ... },\n" +
        "  \"edges\":    [[\"A\",\"B\"], [\"B\",\"C\"], ...],\n" +
        "  \"faces\":    [ { \"pts\":[\"A\",\"B\",\"M\"], \"color\":\"#4488ff\", \"opacity\":0.38, \"label\":\"SAB\" } ],\n" +
        "  \"helpers\":  [\n" +
        "    { \"kind\":\"dashed\",      \"from\":\"S\",  \"to\":\"N\",                   \"color\":\"#ff8800\" },\n" +
        "    { \"kind\":\"line\",        \"from\":\"A\",  \"to\":\"B\",                   \"color\":\"#ffffff\" },\n" +
        "    { \"kind\":\"dot\",         \"at\":\"N\",                                  \"color\":\"#ffee00\" },\n" +
        "    { \"kind\":\"arc\",         \"center\":\"M\",\"from\":\"N\",\"to\":\"S\",  \"radius\":1.2, \"color\":\"#ff4444\", \"label\":\"α\" },\n" +
        "    { \"kind\":\"right_angle\", \"corner\":\"N\",\"d1\":\"M\",\"d2\":\"S\",    \"size\":0.5,  \"color\":\"#aaaaaa\" }\n" +
        "  ],\n" +
        "  \"angles\":   [ { \"face\":\"SAB ∩ основа\", \"value\":\"60°\", \"color\":\"#ff4444\" } ],\n" +
        "  \"camera\":   { \"rotX\":-0.35, \"rotY\":0.55, \"zoom\":1.0 }\n" +
        "}\n" +
        "Rules:\n" +
        "- vertices: ALL labeled points including auxiliary ones (midpoints M, feet of altitudes N/H, etc.) " +
        "with exact computed coordinates.\n" +
        "- edges: only the solid edges of the main shape (not helpers or diagonals).\n" +
        "- faces: each relevant face as an ordered polygon. Give each a distinct color.\n" +
        "- helpers: 'dashed' for altitude/projection lines; 'dot' for special points; " +
        "'arc' to mark the angle itself visually; 'right_angle' for 90° marks.\n" +
        "- angles: one entry per key angle in the problem — these drive the legend and face-toggle buttons.\n" +
        "- camera.rotX/rotY: choose values that show the relevant geometry clearly from the start.\n" +
        "Example — square pyramid SABCD (base 6, height 8), angle of face SAB with base:\n" +
        "{\"vertices\":{\"A\":[0,0,0],\"B\":[6,0,0],\"C\":[6,6,0],\"D\":[0,6,0],\"S\":[3,3,8],\"M\":[3,0,0],\"N\":[3,3,0]}," +
        "\"edges\":[[\"A\",\"B\"],[\"B\",\"C\"],[\"C\",\"D\"],[\"D\",\"A\"],[\"S\",\"A\"],[\"S\",\"B\"],[\"S\",\"C\"],[\"S\",\"D\"]]," +
        "\"faces\":[{\"pts\":[\"S\",\"A\",\"B\"],\"color\":\"#4488ff\",\"opacity\":0.4,\"label\":\"SAB\"}," +
        "{\"pts\":[\"A\",\"B\",\"C\",\"D\"],\"color\":\"#44aa88\",\"opacity\":0.25,\"label\":\"base\"}]," +
        "\"helpers\":[{\"kind\":\"dashed\",\"from\":\"S\",\"to\":\"N\",\"color\":\"#ff8800\"}," +
        "{\"kind\":\"dashed\",\"from\":\"N\",\"to\":\"M\",\"color\":\"#ff8800\"}," +
        "{\"kind\":\"line\",\"from\":\"S\",\"to\":\"M\",\"color\":\"#ffffff\"}," +
        "{\"kind\":\"dot\",\"at\":\"M\",\"color\":\"#ffee00\"},{\"kind\":\"dot\",\"at\":\"N\",\"color\":\"#ffee00\"}," +
        "{\"kind\":\"right_angle\",\"corner\":\"N\",\"d1\":\"M\",\"d2\":\"S\",\"size\":0.5,\"color\":\"#aaaaaa\"}," +
        "{\"kind\":\"arc\",\"center\":\"M\",\"from\":\"N\",\"to\":\"S\",\"radius\":1.2,\"color\":\"#ff4444\",\"label\":\"α\"}]," +
        "\"angles\":[{\"face\":\"SAB ∩ основа\",\"value\":\"≈69.4°\",\"color\":\"#ff4444\"}]," +
        "\"camera\":{\"rotX\":-0.3,\"rotY\":0.5,\"zoom\":1.0}}";

    // ── Extracts the JSON between <STEREO> and </STEREO> tags ─────────────
    public static string? ExtractSceneJson(string llmResponse)
    {
        var match = Regex.Match(llmResponse, @"<STEREO>([\s\S]*?)</STEREO>", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
