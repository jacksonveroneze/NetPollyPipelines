using System.Text.Json.Serialization;

namespace WebApplicationReload.Models;

public class QuotationValue
{
    [JsonPropertyName("value")] 
    public decimal? Value { get; set; }
}