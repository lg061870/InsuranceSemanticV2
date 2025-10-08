//using System.Threading.Tasks;
//using InsuranceAgent.Services;
//using Microsoft.AspNetCore.Mvc;

//namespace InsuranceAgent.Controllers
//{
//    /// <summary>
//    /// API controller for insurance agent conversation endpoints.
//    /// </summary>
//    [ApiController]
//    [Route("api/[controller]")]
//    public class ConversationController : ControllerBase
//    {
//        private readonly InsuranceAgentService _agentService;

//        /// <summary>
//        /// Initializes a new instance of the <see cref="ConversationController"/> class.
//        /// </summary>
//        /// <param name="agentService">The insurance agent service.</param>
//        public ConversationController(InsuranceAgentService agentService)
//        {
//            _agentService = agentService;
//        }

//        /// <summary>
//        /// Processes a message from the user and returns a response.
//        /// </summary>
//        /// <param name="message">The message from the user.</param>
//        /// <returns>The agent response.</returns>
//        [HttpPost("message")]
//        public async Task<IActionResult> ProcessMessage([FromBody] MessageRequest message)
//        {
//            if (string.IsNullOrEmpty(message.Text))
//            {
//                return BadRequest("Message text cannot be empty.");
//            }

//            var response = await _agentService.ProcessMessageAsync(message.Text);

//            return Ok(new MessageResponse { Text = response.Content });
//        }
//    }

//    /// <summary>
//    /// Represents a message request from the user.
//    /// </summary>
//    public class MessageRequest
//    {
//        /// <summary>
//        /// Gets or sets the message text.
//        /// </summary>
//        public string Text { get; set; }
//    }

//    /// <summary>
//    /// Represents a message response from the agent.
//    /// </summary>
//    public class MessageResponse
//    {
//        /// <summary>
//        /// Gets or sets the response text.
//        /// </summary>
//        public string Text { get; set; }
//    }
//}