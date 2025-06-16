using System;
using System.Collections.Generic;

namespace FinserveNew;

public partial class Claim
{
    public int Id { get; set; }

    public string ClaimType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal ClaimAmount { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? SupportingDocumentPath { get; set; }

    public string? SupportingDocumentName { get; set; }

    public string Status { get; set; } = null!;

    public int UserId { get; set; }

    public virtual User User { get; set; } = null!;
}
