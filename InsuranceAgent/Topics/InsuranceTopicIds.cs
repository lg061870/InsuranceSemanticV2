namespace InsuranceAgent.Topics;

/// <summary>Stable descriptor and trigger IDs for the supported InsuranceAgent topic paths.</summary>
public static class InsuranceTopicIds
{
    public const string ConversationStart = "insurance.conversation.start";
    public const string MarketingT1 = "insurance.marketing.t1";
    public const string MarketingT2 = "insurance.marketing.t2";
    public const string MarketingT3 = "insurance.marketing.t3";

    /// <summary>Gets every topic ID required by the insurance start composition.</summary>
    public static IReadOnlyList<string> RequiredRuntimeTopics { get; } =
    [
        ConversationStart,
        MarketingT1,
        MarketingT2,
        MarketingT3,
        "ComplianceTopic",
        "ContactInfoTopic",
        "InsuranceContextTopic",
        "BeneficiaryInfoDemoTopic",
        "CaliforniaResidentTopic"
    ];
}
