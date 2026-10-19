using System;
using System.ComponentModel.DataAnnotations.Schema;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ProjectAppointment : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public DateTime AppointmentDate { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "Beklemede"; 
    // Statuses: "Beklemede", "AlternatifTarihOnerildi", "Onaylandi", "Reddedildi", "IptalEdildi", "Tamamlandi"

    public DateTime? SuggestedAlternativeDate { get; set; }
    public string? AdminNotes { get; set; }

    [NotMapped]
    public DateTime PreferredDate
    {
        get => AppointmentDate;
        set => AppointmentDate = value;
    }

    [NotMapped]
    public string PreferredTimeSlot
    {
        get => AppointmentDate.ToString("HH:mm");
        set { /* optional */ }
    }

    [NotMapped]
    public DateTime? AlternativeDate
    {
        get => SuggestedAlternativeDate;
        set => SuggestedAlternativeDate = value;
    }
}
