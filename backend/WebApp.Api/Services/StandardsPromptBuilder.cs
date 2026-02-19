using System.Text;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

public class StandardsPromptBuilder
{
    private readonly IConfiguration _configuration;

    public StandardsPromptBuilder(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string BuildPolicyPrompt(PolicyConfig? policyConfig, IReadOnlyList<StandardSelection> standards)
    {
        var defaults = GetPolicyDefaults();
        var docType = policyConfig?.DocType ?? defaults.DocType;
        var validationMode = policyConfig?.ValidationMode ?? defaults.ValidationMode;
        var scoringMethod = policyConfig?.ScoringMethod ?? defaults.ScoringMethod;
        var mandatoryWeight = policyConfig?.MandatoryWeight ?? defaults.MandatoryWeight;
        var nonMandatoryWeight = policyConfig?.NonMandatoryWeight ?? defaults.NonMandatoryWeight;
        var criticalFailsImmediate = policyConfig?.CriticalFailsImmediate ?? defaults.CriticalFailsImmediate;
        var maxMajorBeforeFail = policyConfig?.MaxMajorBeforeFail ?? defaults.MaxMajorBeforeFail;
        var scoringNotes = policyConfig?.ScoringNotes ?? defaults.ScoringNotes;
        var runId = policyConfig?.RunId ?? Guid.NewGuid().ToString();

        var sortedStandards = standards
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.StandardId)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("POLICY");
        sb.AppendLine();
        sb.AppendLine("Document type:");
        sb.AppendLine($"- doc_type = \"{docType}\"");
        sb.AppendLine();
        sb.AppendLine("Selected standards to validate against (ordered by priority):");

        foreach (var standard in sortedStandards)
        {
            sb.AppendLine($"- standard_id = \"{standard.StandardId}\"");
            sb.AppendLine($"  title = \"{standard.Title ?? standard.StandardId}\"");
            sb.AppendLine($"  version = \"{standard.Version ?? "unknown"}\"");
            sb.AppendLine($"  jurisdiction = \"{standard.Jurisdiction ?? "unknown"}\"");
            sb.AppendLine($"  priority = {standard.Priority}");
            sb.AppendLine($"  mandatory = {standard.Mandatory.ToString().ToLowerInvariant()}");
        }

        sb.AppendLine();
        sb.AppendLine("Validation mode:");
        sb.AppendLine($"- mode = \"{validationMode}\"");
        sb.AppendLine();
        sb.AppendLine("Scoring:");
        sb.AppendLine($"- scoring_method = \"{scoringMethod}\"");
        sb.AppendLine("- weights:");
        sb.AppendLine($"  - mandatory_weight = {mandatoryWeight}");
        sb.AppendLine($"  - non_mandatory_weight = {nonMandatoryWeight}");
        sb.AppendLine("- fail_thresholds:");
        sb.AppendLine($"  - critical_fails_immediate = {criticalFailsImmediate.ToString().ToLowerInvariant()}");
        sb.AppendLine($"  - max_major_before_fail = {maxMajorBeforeFail}");
        sb.AppendLine("- notes:");
        sb.AppendLine($"  - \"{scoringNotes}\"");
        sb.AppendLine();
        sb.AppendLine("Output requirements:");
        sb.AppendLine("- response must include:");
        sb.AppendLine("  1) Clarification Questions (as tasks)");
        sb.AppendLine("  2) Compliance Score (with calculation notes)");
        sb.AppendLine("  3) Structured List of Non-Compliant/Missing Topics");
        sb.AppendLine("- every finding must include citations grounded in the clauses below");
        sb.AppendLine("- populate citation_document_name and citation fields for each task");
        sb.AppendLine();
        sb.AppendLine("Run metadata:");
        sb.AppendLine($"- run_id = \"{runId}\"");

        if (!string.IsNullOrWhiteSpace(policyConfig?.ProjectProfile ?? defaults.ProjectProfile))
        {
            sb.AppendLine($"- project_profile = \"{policyConfig?.ProjectProfile ?? defaults.ProjectProfile}\"");
        }

        if (!string.IsNullOrWhiteSpace(policyConfig?.CompanyInternalStandardId ?? defaults.CompanyInternalStandardId))
        {
            sb.AppendLine($"- company_internal_standard_id = \"{policyConfig?.CompanyInternalStandardId ?? defaults.CompanyInternalStandardId}\"");
        }

        return sb.ToString();
    }

    public string BuildGroundedClausesPrompt(IReadOnlyList<GroundedClause> clauses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GROUNDED_STANDARDS_CLAUSES");
        sb.AppendLine();
        sb.AppendLine("Rules for use:");
        sb.AppendLine("- Only use the clauses below as evidence.");
        sb.AppendLine("- Every claim must cite at least one clause below using citation_document_name and citation.");
        sb.AppendLine("- If a requirement is not evidenced below, mark citation fields as \"N/A\" and explain the gap.");
        sb.AppendLine();
        sb.AppendLine("Clauses:");

        if (clauses.Count == 0)
        {
            sb.AppendLine("(no clauses retrieved)");
            return sb.ToString();
        }

        foreach (var clause in clauses)
        {
            sb.AppendLine($"[standard_id: {clause.StandardId} | version: {clause.Version ?? "unknown"} | clause_ref: {clause.ClauseRef ?? "n/a"} | source_doc: {clause.SourceDoc}]");
            sb.AppendLine(clause.ClauseText);
        }

        return sb.ToString();
    }

    public string BuildRequirementsFirstPrompt(IReadOnlyList<RequirementItem> requirements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("REQUIREMENTS_FIRST_EVALUATION");
        sb.AppendLine();
        sb.AppendLine("Rules for use:");
        sb.AppendLine("- You MUST produce one task row per requirement listed below.");
        sb.AppendLine("- Use only the uploaded document as evidence.");
        sb.AppendLine("- Do NOT call external tools; rely on the requirements list and the uploaded document only.");
        sb.AppendLine("- Determine verdict using the strongest evidence you can find in the uploaded document:");
        sb.AppendLine("  - Pass: requirement clearly satisfied with direct evidence.");
        sb.AppendLine("  - Partial: requirement partially addressed or ambiguous.");
        sb.AppendLine("  - Fail: requirement contradicted or clearly missing mandatory content.");
        sb.AppendLine("  - NoEvidence: only when no relevant evidence is found at all.");
        sb.AppendLine("- Evidence must quote exact source text when available; use \"N/A\" only when truly no evidence exists.");
        sb.AppendLine("- Output MUST be valid JSON only (no markdown). Use the schema below.");
        sb.AppendLine();
        sb.AppendLine("Required JSON schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"document_name\": \"<uploaded document name>\",");
        sb.AppendLine("  \"id\": \"<uuid>\",");
        sb.AppendLine("  \"response\": \"full evaluation including score + findings + clarification questions\",");
        sb.AppendLine("  \"tasks\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"id\": \"<uuid>\",");
        sb.AppendLine("      \"name\": \"<short task name>\",");
        sb.AppendLine("      \"standard_id\": \"<standard_id>\",");
        sb.AppendLine("      \"clause_ref\": \"<standard clause reference>\",");
        sb.AppendLine("      \"standard_reference\": \"<standard_id + clause_ref>\",");
        sb.AppendLine("      \"requirement_text\": \"<requirement text>\",");
        sb.AppendLine("      \"verdict\": \"Pass|Partial|Fail|NA|Unknown|NoEvidence\",");
        sb.AppendLine("      \"severity\": \"critical|major|minor|info\",");
        sb.AppendLine("      \"evidence\": \"<exact quote from uploaded document or N/A>\",");
        sb.AppendLine("      \"citation_document_name\": \"<document name or N/A>\",");
        sb.AppendLine("      \"citation\": \"<supporting citation text or N/A>\",");
        sb.AppendLine("      \"document_reference\": \"<page/section/key from uploaded doc if known>\",");
        sb.AppendLine("      \"reference\": [\"<exact triggering text from uploaded document>\"],");
        sb.AppendLine("      \"description\": \"<what is missing/non-compliant and why>\",");
        sb.AppendLine("      \"remediation\": \"<what to add/fix if missing>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("- Do not include internal requirement IDs in name, description, evidence, or remediation text.");
        sb.AppendLine();
        sb.AppendLine("Requirements:");

        if (requirements.Count == 0)
        {
            sb.AppendLine("(no requirements provided)");
            return sb.ToString();
        }

        foreach (var requirement in requirements)
        {
            var clauseRef = string.IsNullOrWhiteSpace(requirement.ClauseRef) ? "see source_doc" : requirement.ClauseRef;
            sb.AppendLine($"[standard_id: {requirement.StandardId} | clause_ref: {clauseRef} | source_doc: {requirement.SourceDoc}]");
            sb.AppendLine(requirement.RequirementText);
        }

        return sb.ToString();
    }

    private PolicyDefaults GetPolicyDefaults() => new(
        DocType: _configuration["StandardsPolicy:DocType"] ?? "AIR",
        ValidationMode: _configuration["StandardsPolicy:ValidationMode"] ?? "strict",
        ScoringMethod: _configuration["StandardsPolicy:ScoringMethod"] ?? "weighted_by_priority",
        MandatoryWeight: _configuration.GetValue<double?>("StandardsPolicy:MandatoryWeight") ?? 1.0,
        NonMandatoryWeight: _configuration.GetValue<double?>("StandardsPolicy:NonMandatoryWeight") ?? 0.5,
        CriticalFailsImmediate: _configuration.GetValue<bool?>("StandardsPolicy:CriticalFailsImmediate") ?? true,
        MaxMajorBeforeFail: _configuration.GetValue<int?>("StandardsPolicy:MaxMajorBeforeFail") ?? 0,
        ScoringNotes: _configuration["StandardsPolicy:ScoringNotes"] ?? "Mandatory standards weighted highest.",
        ProjectProfile: _configuration["StandardsPolicy:ProjectProfile"],
        CompanyInternalStandardId: _configuration["StandardsPolicy:CompanyInternalStandardId"]
    );

    private sealed record PolicyDefaults(
        string DocType,
        string ValidationMode,
        string ScoringMethod,
        double MandatoryWeight,
        double NonMandatoryWeight,
        bool CriticalFailsImmediate,
        int MaxMajorBeforeFail,
        string ScoringNotes,
        string? ProjectProfile,
        string? CompanyInternalStandardId
    );
}
