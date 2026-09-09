using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class DentistReferenceFlowTests
{
    [Fact]
    public async Task LookupAndBookingReturnTypedStateAndOptionalHostNotification()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new ConversaCoreBuilder(services)
            .AddTool<LookupTool>(Descriptor("dentist.lookup", ToolSideEffect.ReadOnly))
            .AddTool<BookingTool>(Descriptor("dentist.book", ToolSideEffect.Mutating,
                new ToolConfirmationPolicy { Required = true, Purpose = "book-appointment" },
                new ToolReliabilityPolicy { RequiresIdempotencyKey = true }));
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IToolExecutor>();
        var workflow = new TopicWorkflowContext();
        workflow.SetValue("patient", "Ada");
        var context = new ToolExecutionContext
        {
            ConversationId = "conversation-1", Subject = "patient-1", CorrelationId = "lookup-1",
            Services = provider, AllowedToolIds = new HashSet<string> { "dentist.lookup" }
        };
        var lookup = new InvokeToolActivity<LookupTool, AppointmentRequest, Appointment>(
            "lookup", "dentist.lookup", executor,
            ctx => new AppointmentRequest(ctx.GetValue<string>("patient")!), _ => context, "appointment");
        await lookup.RunAsync(workflow);
        Assert.Equal("Ada", workflow.GetValue<ToolResult<Appointment>>("appointment")!.Value!.Patient);

        var bookingContext = context with
        {
            CorrelationId = "book-1", AllowedToolIds = new HashSet<string> { "dentist.book" },
            ConfirmationGranted = true, TrustedIdentityValidated = true, IdempotencyKey = "book-key-1"
        };
        var booking = new InvokeToolActivity<BookingTool, AppointmentRequest, Booking>(
            "booking", "dentist.book", executor, _ => new AppointmentRequest("Ada"), _ => bookingContext, "booking");
        await booking.RunAsync(workflow);
        Assert.True(workflow.GetValue<ToolResult<Booking>>("booking")!.Value!.Confirmed);

        var notification = new HostNotification<AppointmentBooked>("conversation-1", "appointment.booked", 1,
            new AppointmentBooked("Ada"));
        Assert.Equal("Ada", notification.Payload.Patient);
    }

    private static ToolDescriptor Descriptor(string id, ToolSideEffect sideEffect,
        ToolConfirmationPolicy? confirmation = null, ToolReliabilityPolicy? reliability = null) =>
        new(id, "1", id, id, typeof(AppointmentRequest), sideEffect == ToolSideEffect.ReadOnly ? typeof(Appointment) : typeof(Booking),
            sideEffect: sideEffect, confirmation: confirmation, reliability: reliability);

    private sealed record AppointmentRequest(string Patient);
    private sealed record Appointment(string Patient, string Time = "10:00");
    private sealed record Booking(string Patient, bool Confirmed = true);
    private sealed record AppointmentBooked(string Patient);
    private sealed class LookupTool : IConversaTool<AppointmentRequest, Appointment>
    {
        public ToolDescriptor Descriptor => throw new NotSupportedException();
        public ValueTask<ToolResult<Appointment>> ExecuteAsync(AppointmentRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ToolResult<Appointment>.Success(new Appointment(request.Patient)));
    }
    private sealed class BookingTool : IConversaTool<AppointmentRequest, Booking>
    {
        public ToolDescriptor Descriptor => throw new NotSupportedException();
        public ValueTask<ToolResult<Booking>> ExecuteAsync(AppointmentRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ToolResult<Booking>.Success(new Booking(request.Patient)));
    }
}
