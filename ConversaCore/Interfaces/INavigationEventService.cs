using System;
using System.Threading.Tasks;

namespace ConversaCore.Interfaces;

public interface INavigationEventService
{
    event Func<string, Task>? NavigateRequested;
    Task RequestNavigate(string url);
}