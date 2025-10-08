using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace ConversaCore.Services
{
    public interface IChatInteropService
    {
        Task InitializeChatUIAsync();
        Task ScrollToBottomAsync(ElementReference container);
        Task<Dictionary<string, object>> HandleAdaptiveCardSubmitAsync(string actionType, Dictionary<string, object> data);
    }
}