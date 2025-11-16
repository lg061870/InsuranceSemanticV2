
namespace ConversaCore {
    public interface INoOpVectorDatabaseService {
        Task<List<(string Text, float Score)>> SearchAsync(float[] queryVector, int topK = 5);
        Task StoreAsync(string id, float[] vector, string text, string fileName, Dictionary<string, object> metadata);
    }
}