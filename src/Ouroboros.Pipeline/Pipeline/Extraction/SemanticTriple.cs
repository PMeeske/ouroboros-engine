namespace Ouroboros.Pipeline.Extraction;

/// <summary>
/// Represents a semantic triple extracted from a document.
/// </summary>
/// <param name="Subject">The subject of the triple (e.g., a document).</param>
/// <param name="Predicate">The predicate/relation (e.g., Author, Status, Topic).</param>
/// <param name="Object">The object of the triple (e.g., a user, state, concept).</param>
public sealed record SemanticTriple(string Subject, string Predicate, string Object)
{
    /// <summary>
    /// Converts the triple to a MeTTa fact representation.
    /// </summary>
    /// <returns>The MeTTa fact string.</returns>
    public string ToMeTTaFact() => $"({this.Predicate} (Doc \"{this.Subject}\") ({this.InferObjectType()} \"{this.Object}\"))";

    /// <summary>
    /// Infers the object type based on the predicate.
    /// </summary>
    private string InferObjectType() => this.Predicate switch
    {
        "Author" or "CreatedBy" or "ModifiedBy" => "User",
        "Status" => "State",
        "Topic" or "Contains" => "Concept",
        "References" or "DependsOn" => "Doc",
        _ => "Entity",
    };
}