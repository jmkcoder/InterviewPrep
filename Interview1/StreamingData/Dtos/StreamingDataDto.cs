namespace Interview1.StreamingData.Dtos;

public class StreamingDataDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
