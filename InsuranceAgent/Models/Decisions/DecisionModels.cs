using System.Text.Json;

namespace InsuranceAgent.Models.Decisions;

/// <summary>
/// Example input models for DecisionActivity - these represent rules, context, or reference data
/// </summary>

// Vector store input - for document-based decisions
public class VectorStoreInput
{
    public string CollectionName { get; set; } = string.Empty;
    public string[] SearchQueries { get; set; } = Array.Empty<string>();
    public int MaxResults { get; set; } = 10;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// JSON rules input - for rule-based decisions
public class JsonRulesInput
{
    public string RulesFilePath { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string RulesVersion { get; set; } = "1.0";
}

// Direct rules input - for in-memory rules
public class InlineRulesInput
{
    public List<BusinessRule> Rules { get; set; } = new();
    public Dictionary<string, object> GlobalParameters { get; set; } = new();
    public string Context { get; set; } = string.Empty;
}

public class BusinessRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Conditions { get; set; } = new();
    public Dictionary<string, object> Actions { get; set; } = new();
    public int Priority { get; set; } = 1;
}

/// <summary>
/// Example evidence models - these represent the data to be analyzed
/// </summary>

// Insurance lead information
public class LeadInfo
{
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string SmokingStatus { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal DesiredCoverage { get; set; }
    public string Employment { get; set; } = string.Empty;
    public List<MedicalCondition> MedicalHistory { get; set; } = new();
    public List<LegalIssue> LegalHistory { get; set; } = new();
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

public class MedicalCondition
{
    public string Condition { get; set; } = string.Empty;
    public int DiagnosisAge { get; set; }
    public int YearsAgo { get; set; }
    public bool Controlled { get; set; }
    public int Medications { get; set; }
}

public class LegalIssue
{
    public string Issue { get; set; } = string.Empty;
    public int YearsAgo { get; set; }
    public string Details { get; set; } = string.Empty;
}

// Document analysis evidence
public class DocumentEvidence
{
    public string DocumentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public DateTime DocumentDate { get; set; }
    public string Source { get; set; } = string.Empty;
}

// User conversation evidence
public class ConversationEvidence
{
    public string UserInput { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedEntities { get; set; } = new();
    public List<string> ConversationHistory { get; set; } = new();
    public string CurrentTopic { get; set; } = string.Empty;
}

/// <summary>
/// Example response models - these represent the expected output structure
/// </summary>

// Insurance decision response
public class InsuranceDecisionResponse
{
    public List<CarrierMatch> PerfectMatches { get; set; } = new();
    public List<CarrierMatch> NearMatches { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();
    public string OverallRecommendation { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

public class CarrierMatch
{
    public string CarrierName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public string ExceptionCondition { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}

// Document analysis response
public class DocumentAnalysisResponse
{
    public string DocumentType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<ExtractedField> ExtractedFields { get; set; } = new();
    public List<string> KeyFindings { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public List<string> RequiredActions { get; set; } = new();
}

public class ExtractedField
{
    public string FieldName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Location { get; set; } = string.Empty;
}

// Conversation routing response
public class ConversationRoutingResponse
{
    public string RecommendedTopic { get; set; } = string.Empty;
    public string ResponseMessage { get; set; } = string.Empty;
    public List<string> SuggestedActions { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public Dictionary<string, object> ContextUpdates { get; set; } = new();
}

// Generic classification response
public class ClassificationResponse
{
    public string Category { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public List<CategoryMatch> AlternativeCategories { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CategoryMatch
{
    public string Category { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
}