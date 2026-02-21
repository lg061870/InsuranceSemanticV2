using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace ConversaCore.TopicTool
{
    [Guid("d8f40f9f-9d6f-4c0d-8a02-2e9ad5d0b9d7")]
    public class TopicToolWindow : ToolWindowPane
    {
        public TopicToolWindow() : base(null)
        {
            this.Caption = "ConversaCore Topic Designer";
            this.Content = new TopicManagementControl();
        }
    }
}
