using System.Numerics;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Reflection.Metadata.BlobBuilder;

namespace InsuranceSemanticV2.Core.DTO;

public class AgentCarrierAppointmentRequest {
    public int AgentId { get; set; }
    public int CarrierId { get; set; }
    public string Status { get; set; }
}
