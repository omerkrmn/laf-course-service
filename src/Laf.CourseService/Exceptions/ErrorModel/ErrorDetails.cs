using System.Text.Json;

namespace Laf.CourseService.Exceptions.ErrorModel;

public class ErrorDetails
{
    public int StatusCode { get; set; }
    public string? Message { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
