using System.ComponentModel.DataAnnotations;

namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>Body for <c>PATCH /disputes/{id}/status</c> (TDP-DISP-02 §2.2). Ops-only.</summary>
public sealed record UpdateStatusRequest([property: Required] string Status);
