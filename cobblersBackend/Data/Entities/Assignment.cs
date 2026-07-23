namespace cobblersBackend.Data.Entities;


public enum AssignmentKind
{
    Code,
    Predict,
    Project
}

// Renamed from Task 2026-07-16 to stop colliding with System.Threading.Tasks.Task.
// The rename now covers domain/wire/DB too (CONTRACT.md, SCHEMA.md) — table is
// `assignment`, wire term `assignmentId`. See SCHEMA.md's Assignment section.
public class Assignment
{
    public int Id { get; set; }
    // Stable natural key (kebab-case, e.g. "hello-itu"). Identical across
    // databases while Id is DB-assigned — seed upserts and any per-assignment
    // code hooks key on this, never on Id. Internal only, not exposed on the API.
    public required string Slug { get; set; }
    public required AssignmentKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    // Teaching blocks shown above the task ({kind:"text"|"code", ...}[]).
    // Null = no lesson (e.g. Day 2/3 quizzes). Wire field `lesson` — sibling of
    // hint/content, not folded into ContentJson (see SCHEMA.md / CONTRACT.md).
    public string? LessonJson { get; set; }
    public string? Hint { get; set; }
    public required string ContentJson { get; set; }
    public string? SampleSolutionJson { get; set; }
    // Serializable grading rules (see SCHEMA.md "Grading rules are data").
    // Null = not auto-gradable (projects, NIM) or graded generically (predict).
    public string? GradingJson { get; set; }

    public ICollection<AssignmentSetAssignment> AssignmentSets { get; set; } = [];
    public ICollection<Submission> Submissions { get; set; } = [];

}
