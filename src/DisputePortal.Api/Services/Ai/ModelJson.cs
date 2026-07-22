namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Defensive helpers for reading JSON out of a model's text output (TDP-AI-01 §2.5,
/// TDP-AI-02 §2.5). The models are instructed to "return only valid JSON", but we never
/// trust that blindly: locate the first balanced <c>{ ... }</c> span (ignoring braces inside
/// strings) so leading/trailing prose or code fences cannot break deserialization.
/// </summary>
internal static class ModelJson
{
    /// <summary>
    /// Returns the first balanced top-level JSON object substring, or null if none is found.
    /// </summary>
    public static string? ExtractFirstObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var start = raw.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < raw.Length; i++)
        {
            var ch = raw[i];

            if (inString)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') inString = false;
                continue;
            }

            switch (ch)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0) return raw.Substring(start, i - start + 1);
                    break;
            }
        }

        return null; // unbalanced
    }
}
