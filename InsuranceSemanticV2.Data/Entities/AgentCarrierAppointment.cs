namespace InsuranceSemanticV2.Data.Entities;

// ------------------ AgentCarrierAppointment ------------------

public class AgentCarrierAppointment {
    public int AppointmentId { get; set; }
    public int AgentId { get; set; }
    public int CarrierId { get; set; }

    public string Status { get; set; }

    public Agent Agent { get; set; }
    public Carrier Carrier { get; set; }
}
