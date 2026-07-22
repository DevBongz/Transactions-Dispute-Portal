namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>Body for <c>201 Created</c> from <c>POST /disputes</c> (TDP-DISP-01 §2.2).</summary>
public sealed record SubmitDisputeResponse(Guid Id, string Reference, string Status);
