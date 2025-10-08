
namespace ConversaCore.Services {
    public interface IIntentRecognitionService {
        Task<float> GetTopicConfidenceAsync(string message, string topicName, string context = null);
        Task<(string Intent, float Confidence)> RecognizeIntentAsync(string message, string context = null);
    }
}