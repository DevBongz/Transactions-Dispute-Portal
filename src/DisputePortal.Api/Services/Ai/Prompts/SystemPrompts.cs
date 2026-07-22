namespace DisputePortal.Api.Services.Ai.Prompts;

/// <summary>
/// System prompt constants for the three AI features, copied <em>verbatim</em> from
/// SPEC §3.5 (Features 1–3). Keeping them as source constants makes the prompt contract
/// reviewable and stable; do not reword without a SPEC change.
/// </summary>
public static class SystemPrompts
{
    /// <summary>SPEC §3.5, Feature 1 — natural-language dispute extraction.</summary>
    public const string Extraction =
        "You are a dispute intake assistant for a bank. Extract structured dispute fields from the customer's description.\n" +
        "Return a JSON object with these optional fields: transactionRef, category (one of UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER), amount (number), merchantName, transactionDate (ISO8601 date), and a confidence map (0.0–1.0 per field).\n" +
        "If a field cannot be determined, omit it. Return only valid JSON.";

    /// <summary>SPEC §3.5, Feature 2 — intelligent dispute classification.</summary>
    public const string Classification =
        "You are a financial dispute triage engine. Classify the following dispute.\n" +
        "Return a JSON object: { \"category\": \"<CATEGORY>\", \"priority\": \"<PRIORITY>\", \"rationale\": \"<one sentence>\" }\n" +
        "Category must be one of: UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER.\n" +
        "Priority must be one of: LOW, MEDIUM, HIGH, CRITICAL.\n" +
        "Base priority on: amount (>R5000 = HIGH baseline), category (UNAUTHORISED = bump one level), and any prior open disputes by this customer.\n" +
        "Return only valid JSON.";

    /// <summary>SPEC §3.5, Feature 3 — customer-facing resolution summary.</summary>
    public const string ResolutionSummary =
        "You are a customer communication specialist at a bank. Write a clear, empathetic, plain-language summary (2–4 sentences) for the customer explaining the outcome of their transaction dispute.\n" +
        "Do not use jargon. Do not reveal internal investigation details. Be specific about the outcome.\n" +
        "Return only the summary text, no JSON wrapper.";
}
