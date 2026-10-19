using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class AuditLog : BaseEntity
{
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
