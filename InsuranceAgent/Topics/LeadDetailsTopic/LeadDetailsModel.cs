using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;

namespace InsuranceAgent.Topics.LeadDetailsTopic
{
    /// <summary>
    /// Model for lead details and sales tracking information.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    public class LeadDetailsModel : BaseCardModel
    {
        [JsonPropertyName("lead_name")]
        [Required(ErrorMessage = "Lead name is required")]
        [StringLength(100, ErrorMessage = "Lead name cannot exceed 100 characters")]
        public string? LeadName { get; set; }

        [JsonPropertyName("language")]
        [StringLength(50, ErrorMessage = "Language cannot exceed 50 characters")]
        public string? Language { get; set; }

        [JsonPropertyName("lead_source")]
        [Required(ErrorMessage = "Lead source is required")]
        [StringLength(100, ErrorMessage = "Lead source cannot exceed 100 characters")]
        public string? LeadSource { get; set; }

        [JsonPropertyName("interest_level")]
        [Required(ErrorMessage = "Interest level is required")]
        [StringLength(20, ErrorMessage = "Interest level cannot exceed 20 characters")]
        public string? InterestLevel { get; set; }

        [JsonPropertyName("lead_intent")]
        [Required(ErrorMessage = "Lead intent is required")]
        [StringLength(50, ErrorMessage = "Lead intent cannot exceed 50 characters")]
        public string? LeadIntent { get; set; }

        [JsonPropertyName("appointment_date_time")]
        [JsonIgnore] // Internal field - not for customer input
        [StringLength(50, ErrorMessage = "Appointment date/time cannot exceed 50 characters")]
        public string? AppointmentDateTime { get; set; }

        [JsonPropertyName("follow_up_needed")]
        [JsonIgnore] // Internal field - not for customer input
        public string FollowUpNeededString { get; set; } = "false";

        [JsonPropertyName("notes_for_sales_agent")]
        [JsonIgnore] // Internal field - not for customer input
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? NotesForSalesAgent { get; set; }

        [JsonPropertyName("lead_url")]
        [JsonIgnore] // Internal field - not for customer input
        [StringLength(200, ErrorMessage = "Lead URL cannot exceed 200 characters")]
        public string? LeadUrl { get; set; }

        // Computed properties for business logic
        public bool? FollowUpNeeded
        {
            get
            {
                if (FollowUpNeededString?.ToLower() == "true") return true;
                if (FollowUpNeededString?.ToLower() == "false") return false;
                return null;
            }
        }

        // Validation properties
        public bool HasProvidedLeadName => !string.IsNullOrWhiteSpace(LeadName);
        public bool HasProvidedLanguage => !string.IsNullOrWhiteSpace(Language);
        public bool HasProvidedLeadSource => !string.IsNullOrWhiteSpace(LeadSource);
        public bool HasProvidedInterestLevel => !string.IsNullOrWhiteSpace(InterestLevel);
        public bool HasProvidedLeadIntent => !string.IsNullOrWhiteSpace(LeadIntent);
        public bool HasProvidedAppointment => !string.IsNullOrWhiteSpace(AppointmentDateTime);
        public bool HasProvidedNotes => !string.IsNullOrWhiteSpace(NotesForSalesAgent);
        public bool HasProvidedLeadUrl => !string.IsNullOrWhiteSpace(LeadUrl);

        // Interest level analysis
        public string NormalizedInterestLevel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InterestLevel)) return "Unknown";
                
                var level = InterestLevel.Trim().ToLowerInvariant();
                return level switch
                {
                    "high" or "very high" or "hot" or "urgent" => "High",
                    "medium" or "moderate" or "warm" => "Medium", 
                    "low" or "cold" or "minimal" => "Low",
                    _ => InterestLevel.Trim()
                };
            }
        }

        public int InterestLevelScore => NormalizedInterestLevel switch
        {
            "High" => 100,
            "Medium" => 60,
            "Low" => 20,
            _ => 40 // Unknown/Other
        };

        // Lead intent analysis
        public string NormalizedLeadIntent
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LeadIntent)) return "Unknown";
                
                var intent = LeadIntent.Trim().ToLowerInvariant();
                return intent switch
                {
                    "buy" or "purchase" or "apply" or "ready" => "Buy",
                    "learn" or "research" or "explore" or "information" => "Learn",
                    "compare" or "shop" or "quote" or "pricing" => "Compare",
                    "callback" or "follow up" or "schedule" => "Schedule",
                    _ => LeadIntent.Trim()
                };
            }
        }

        public int LeadIntentScore => NormalizedLeadIntent switch
        {
            "Buy" => 100,
            "Schedule" => 85,
            "Compare" => 70,
            "Learn" => 40,
            _ => 30 // Unknown/Other
        };

        // Lead source analysis
        public string NormalizedLeadSource
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LeadSource)) return "Unknown";
                
                var source = LeadSource.Trim().ToLowerInvariant();
                return source switch
                {
                    var s when s.Contains("website") || s.Contains("web") || s.Contains("online") => "Website",
                    var s when s.Contains("referral") || s.Contains("refer") => "Referral",
                    var s when s.Contains("social") || s.Contains("facebook") || s.Contains("linkedin") => "Social Media",
                    var s when s.Contains("phone") || s.Contains("call") => "Phone",
                    var s when s.Contains("email") || s.Contains("newsletter") => "Email",
                    var s when s.Contains("ad") || s.Contains("advertisement") || s.Contains("google") => "Advertising",
                    var s when s.Contains("event") || s.Contains("conference") || s.Contains("seminar") => "Event",
                    _ => LeadSource.Trim()
                };
            }
        }

        public int LeadSourceQualityScore => NormalizedLeadSource switch
        {
            "Referral" => 95,
            "Website" => 80,
            "Phone" => 75,
            "Email" => 65,
            "Event" => 60,
            "Social Media" => 50,
            "Advertising" => 45,
            _ => 35 // Unknown/Other
        };

        // Language preference analysis
        public string NormalizedLanguage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Language)) return "English"; // Default assumption
                
                var lang = Language.Trim().ToLowerInvariant();
                return lang switch
                {
                    "english" or "en" => "English",
                    "spanish" or "español" or "es" => "Spanish",
                    "french" or "français" or "fr" => "French",
                    "german" or "deutsch" or "de" => "German",
                    "italian" or "italiano" or "it" => "Italian",
                    "portuguese" or "português" or "pt" => "Portuguese",
                    "chinese" or "mandarin" or "zh" => "Chinese",
                    _ => Language.Trim()
                };
            }
        }

        public bool RequiresSpecializedSupport => 
            !string.Equals(NormalizedLanguage, "English", StringComparison.OrdinalIgnoreCase);

        // Appointment analysis
        public DateTime? ParsedAppointmentDateTime
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AppointmentDateTime)) return null;
                
                // Try multiple date formats
                var formats = new[]
                {
                    "yyyy-MM-dd HH:mm",
                    "yyyy-MM-dd H:mm",
                    "M/d/yyyy H:mm",
                    "M/d/yyyy HH:mm",
                    "MM/dd/yyyy HH:mm",
                    "dd/MM/yyyy HH:mm",
                    "yyyy-MM-ddTHH:mm:ss",
                    "yyyy-MM-ddTHH:mm"
                };
                
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(AppointmentDateTime.Trim(), format, 
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
                
                // Fallback to general parsing
                if (DateTime.TryParse(AppointmentDateTime.Trim(), out var fallback))
                {
                    return fallback;
                }
                
                return null;
            }
        }

        public bool HasValidAppointment => ParsedAppointmentDateTime.HasValue;
        
        public bool IsAppointmentInFuture => 
            ParsedAppointmentDateTime.HasValue && ParsedAppointmentDateTime.Value > DateTime.Now;
            
        public bool IsAppointmentToday =>
            ParsedAppointmentDateTime.HasValue && ParsedAppointmentDateTime.Value.Date == DateTime.Today;
            
        public bool IsAppointmentThisWeek =>
            ParsedAppointmentDateTime.HasValue && 
            ParsedAppointmentDateTime.Value >= DateTime.Today &&
            ParsedAppointmentDateTime.Value < DateTime.Today.AddDays(7);

        // URL validation
        public bool HasValidLeadUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LeadUrl)) return false;
                return Uri.TryCreate(LeadUrl, UriKind.Absolute, out var uri) && 
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
        }

        // Data completeness scoring
        public int LeadDetailsDataQualityScore
        {
            get
            {
                var score = 0;
                if (HasProvidedLeadName) score += 20; // Most important
                if (HasProvidedInterestLevel) score += 15;
                if (HasProvidedLeadIntent) score += 15;
                if (HasProvidedLeadSource) score += 15;
                if (HasProvidedLanguage) score += 10;
                if (HasProvidedAppointment) score += 10;
                if (FollowUpNeeded.HasValue) score += 5;
                if (HasProvidedNotes) score += 5;
                if (HasProvidedLeadUrl) score += 5;
                return score;
            }
        }

        public string LeadDetailsDataQualityGrade => LeadDetailsDataQualityScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        // Lead qualification composite score
        public int LeadQualificationScore
        {
            get
            {
                var baseScore = LeadDetailsDataQualityScore;
                var interestWeight = InterestLevelScore * 0.3;
                var intentWeight = LeadIntentScore * 0.3;
                var sourceWeight = LeadSourceQualityScore * 0.2;
                var urgencyBonus = 0;
                
                if (IsAppointmentToday) urgencyBonus += 10;
                else if (IsAppointmentThisWeek) urgencyBonus += 5;
                
                if (FollowUpNeeded == true) urgencyBonus += 5;
                
                var totalScore = baseScore + interestWeight + intentWeight + sourceWeight + urgencyBonus;
                return Math.Min((int)totalScore, 100);
            }
        }

        public string LeadQualificationGrade => LeadQualificationScore switch
        {
            >= 90 => "A+",
            >= 80 => "A", 
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        // Sales priority analysis
        public string SalesPriorityLevel
        {
            get
            {
                if (NormalizedLeadIntent == "Buy" && NormalizedInterestLevel == "High") return "Critical";
                if (IsAppointmentToday || (NormalizedLeadIntent == "Buy")) return "Urgent";
                if (IsAppointmentThisWeek || NormalizedInterestLevel == "High") return "High";
                if (NormalizedLeadIntent == "Compare" || NormalizedInterestLevel == "Medium") return "Medium";
                return "Standard";
            }
        }

        // Action items and next steps
        public List<string> RecommendedActions
        {
            get
            {
                var actions = new List<string>();
                
                if (IsAppointmentToday)
                    actions.Add("🚨 URGENT: Appointment scheduled for today");
                else if (IsAppointmentThisWeek)
                    actions.Add("⏰ Priority: Appointment this week");
                    
                if (NormalizedLeadIntent == "Buy")
                    actions.Add("💰 Ready to Buy: Prepare application materials");
                    
                if (RequiresSpecializedSupport)
                    actions.Add($"🌍 Language Support: Assign {NormalizedLanguage}-speaking agent");
                    
                if (FollowUpNeeded == true)
                    actions.Add("📞 Follow-up Required: Schedule callback");
                    
                if (NormalizedLeadSource == "Referral")
                    actions.Add("⭐ Referral Lead: High-priority handling");
                    
                if (LeadQualificationScore >= 80)
                    actions.Add("🎯 High-Quality Lead: Assign senior agent");
                    
                return actions;
            }
        }

        // Lead management insights
        public List<string> LeadInsights
        {
            get
            {
                var insights = new List<string>();
                
                insights.Add($"Quality Score: {LeadQualificationScore}/100 (Grade {LeadQualificationGrade})");
                insights.Add($"Priority Level: {SalesPriorityLevel}");
                insights.Add($"Source Quality: {NormalizedLeadSource} ({LeadSourceQualityScore}/100)");
                insights.Add($"Interest × Intent: {NormalizedInterestLevel} × {NormalizedLeadIntent}");
                
                if (RequiresSpecializedSupport)
                    insights.Add($"Language Requirement: {NormalizedLanguage} support needed");
                    
                if (HasValidAppointment)
                    insights.Add($"Appointment Status: {ParsedAppointmentDateTime:yyyy-MM-dd HH:mm}");
                    
                return insights;
            }
        }
    }
}