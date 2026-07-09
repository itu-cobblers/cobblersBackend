using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cobblersBackend.Data.Entities;

public class Session
{

    public required string SessionId { get; set; }
    public required string Code { get; set; }
    public required int Year { get; set; }
    public required string TaskSetId { get; set; }
    public DateTimeOffset CreateDateTime { get; set; }

    public required TaskSet TaskSet { get; set; }

}