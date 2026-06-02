namespace JobScheduler.DAL.Models;

public class JobParameter
{
    public long ParameterId { get; set; }
    public Guid JobId { get; set; }
    public string ParameterSet { get; set; } = "default";
    public string ParameterName { get; set; } = string.Empty;
    public string ParameterValue { get; set; } = string.Empty;
    public string ParameterType { get; set; } = "string";
    public bool IsSensitive { get; set; } = false;
    public bool IsRequired { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
