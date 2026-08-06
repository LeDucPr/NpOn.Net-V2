using MongoDB.Bson;
using MongoDB.Driver;

namespace Common.Infrastructures.NpOn.MongoDbExtCm.Bsons;

// ─────────────────────────────────────────────────────────────────────────────
//  Join descriptor  ($lookup)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Describes a $lookup (join) stage between two MongoDB collections.
/// Mongo supports Inner / Left-outer joins natively; Right / Full are
/// emulated via pipeline-style lookups.
/// </summary>
public sealed class MongoJoinDescriptor
{
    /// <summary>The foreign collection to join with.</summary>
    public string FromCollection { get; }

    /// <summary>Field in the current (local) documents.</summary>
    public string LocalField { get; }

    /// <summary>Matching field in the foreign collection.</summary>
    public string ForeignField { get; }

    /// <summary>Alias for the joined array that will appear in results.</summary>
    public string AsAlias { get; }

    /// <summary>
    /// Optional inner-pipeline to filter / project foreign docs before joining.
    /// When provided, the builder emits the extended pipeline-style $lookup.
    /// </summary>
    public IReadOnlyList<BsonDocument>? InnerPipeline { get; }

    /// <summary>
    /// When <c>true</c> the builder adds a $unwind after $lookup so the
    /// result looks like a flat SQL inner-join (each doc × matched foreign doc).
    /// </summary>
    public bool UnwindResult { get; }

    /// <summary>
    /// When <c>true</c>, <c>null</c> / missing join results are preserved
    /// (left-outer semantics). Ignored if <see cref="UnwindResult"/> is false.
    /// </summary>
    public bool PreserveNullAndEmpty { get; }

    public MongoJoinDescriptor(
        string fromCollection,
        string localField,
        string foreignField,
        string asAlias,
        IReadOnlyList<BsonDocument>? innerPipeline = null,
        bool unwindResult = false,
        bool preserveNullAndEmpty = true)
    {
        FromCollection    = fromCollection;
        LocalField        = localField;
        ForeignField      = foreignField;
        AsAlias           = asAlias;
        InnerPipeline     = innerPipeline;
        UnwindResult      = unwindResult;
        PreserveNullAndEmpty = preserveNullAndEmpty;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Aggregate pipeline builder
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Fluent builder that constructs a MongoDB aggregation pipeline as a
/// <see cref="List{BsonDocument}"/> of stage documents.
/// <para>
/// Usage example:
/// <code>
/// var pipeline = new MongoAggregateBuilder()
///     .Match(new BsonDocument("status", "active"))
///     .LookupJoin(new MongoJoinDescriptor("orders", "userId", "_id", "orders", unwindResult: true))
///     .Project(new BsonDocument { { "name", 1 }, { "orders.total", 1 } })
///     .Sort(new BsonDocument("name", 1))
///     .Limit(50)
///     .Build();
/// </code>
/// </para>
/// </summary>
public sealed class MongoAggregateBuilder
{
    private readonly List<BsonDocument> _stages = new();

    // ── Filter ────────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$match</c> stage.</summary>
    public MongoAggregateBuilder Match(BsonDocument filter)
    {
        _stages.Add(new BsonDocument("$match", filter));
        return this;
    }

    /// <summary>Adds a <c>$match</c> stage from a raw JSON filter string.</summary>
    public MongoAggregateBuilder Match(string filterJson)
        => Match(BsonDocument.Parse(filterJson));

    // ── Projection ────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$project</c> stage.</summary>
    public MongoAggregateBuilder Project(BsonDocument projection)
    {
        _stages.Add(new BsonDocument("$project", projection));
        return this;
    }

    /// <summary>Convenience: include / exclude fields by name.</summary>
    public MongoAggregateBuilder Project(IEnumerable<string> includeFields,
        IEnumerable<string>? excludeFields = null)
    {
        var doc = new BsonDocument();
        foreach (var f in includeFields)    doc.Add(f, 1);
        foreach (var f in excludeFields ?? []) doc.Add(f, 0);
        return Project(doc);
    }

    // ── Sorting ───────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$sort</c> stage.</summary>
    public MongoAggregateBuilder Sort(BsonDocument sort)
    {
        _stages.Add(new BsonDocument("$sort", sort));
        return this;
    }

    /// <summary>Sort by a single field. <paramref name="ascending"/> = true → 1, false → -1.</summary>
    public MongoAggregateBuilder SortBy(string field, bool ascending = true)
        => Sort(new BsonDocument(field, ascending ? 1 : -1));

    // ── Paging ────────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$skip</c> stage.</summary>
    public MongoAggregateBuilder Skip(int count)
    {
        _stages.Add(new BsonDocument("$skip", count));
        return this;
    }

    /// <summary>Adds a <c>$limit</c> stage.</summary>
    public MongoAggregateBuilder Limit(int count)
    {
        _stages.Add(new BsonDocument("$limit", count));
        return this;
    }

    /// <summary>Adds both <c>$skip</c> and <c>$limit</c> for page-based paging.</summary>
    public MongoAggregateBuilder Page(int pageIndex, int pageSize)
        => Skip(pageIndex * pageSize).Limit(pageSize);

    // ── Grouping ──────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$group</c> stage.</summary>
    public MongoAggregateBuilder Group(BsonDocument groupStage)
    {
        _stages.Add(new BsonDocument("$group", groupStage));
        return this;
    }

    /// <summary>
    /// Convenience group: group by a single field with accumulator expressions.
    /// <para>Example: <c>GroupBy("category", new BsonDocument("total", new BsonDocument("$sum", "$price")))</c></para>
    /// </summary>
    public MongoAggregateBuilder GroupBy(string groupByField, BsonDocument? accumulators = null)
    {
        var groupDoc = new BsonDocument("_id", $"${groupByField}");
        if (accumulators != null)
            foreach (var element in accumulators)
                groupDoc.Add(element);
        return Group(groupDoc);
    }

    // ── Unwind ────────────────────────────────────────────────────────────────

    /// <summary>Adds a <c>$unwind</c> stage.</summary>
    public MongoAggregateBuilder Unwind(string arrayField, bool preserveNullAndEmpty = false)
    {
        var unwind = new BsonDocument
        {
            { "path", $"${arrayField}" },
            { "preserveNullAndEmptyArrays", preserveNullAndEmpty }
        };
        _stages.Add(new BsonDocument("$unwind", unwind));
        return this;
    }

    // ── Join ($lookup) ────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a <c>$lookup</c> stage (and optional <c>$unwind</c>) from a
    /// <see cref="MongoJoinDescriptor"/>.
    /// </summary>
    public MongoAggregateBuilder LookupJoin(MongoJoinDescriptor join)
    {
        BsonDocument lookupDoc;

        if (join.InnerPipeline is { Count: > 0 })
        {
            // Extended pipeline-style $lookup (supports sub-filters / projections)
            lookupDoc = new BsonDocument("$lookup", new BsonDocument
            {
                { "from",     join.FromCollection },
                { "let",      new BsonDocument(join.LocalField.Replace(".", "_"), $"${join.LocalField}") },
                { "pipeline", new BsonArray(join.InnerPipeline) },
                { "as",       join.AsAlias }
            });
        }
        else
        {
            // Classic equality $lookup
            lookupDoc = new BsonDocument("$lookup", new BsonDocument
            {
                { "from",         join.FromCollection },
                { "localField",   join.LocalField     },
                { "foreignField", join.ForeignField   },
                { "as",           join.AsAlias        }
            });
        }

        _stages.Add(lookupDoc);

        if (join.UnwindResult)
            Unwind(join.AsAlias, join.PreserveNullAndEmpty);

        return this;
    }

    /// <summary>
    /// Shortcut for a simple left-join: <c>$lookup</c> without unwind
    /// (result array stays embedded).
    /// </summary>
    public MongoAggregateBuilder LeftJoin(
        string fromCollection,
        string localField,
        string foreignField,
        string asAlias)
        => LookupJoin(new MongoJoinDescriptor(fromCollection, localField, foreignField, asAlias));

    /// <summary>
    /// Shortcut for an "inner-join" emulation: <c>$lookup</c> + <c>$unwind</c>
    /// (drops documents with no match).
    /// </summary>
    public MongoAggregateBuilder InnerJoin(
        string fromCollection,
        string localField,
        string foreignField,
        string asAlias)
        => LookupJoin(new MongoJoinDescriptor(
            fromCollection, localField, foreignField, asAlias,
            unwindResult: true, preserveNullAndEmpty: false));

    // ── Add / Replace fields ──────────────────────────────────────────────────

    /// <summary>Adds a <c>$addFields</c> stage.</summary>
    public MongoAggregateBuilder AddFields(BsonDocument fields)
    {
        _stages.Add(new BsonDocument("$addFields", fields));
        return this;
    }

    /// <summary>Adds a <c>$replaceRoot</c> stage.</summary>
    public MongoAggregateBuilder ReplaceRoot(string newRootExpression)
    {
        _stages.Add(new BsonDocument("$replaceRoot",
            new BsonDocument("newRoot", newRootExpression)));
        return this;
    }

    // ── Facet (multi-pipeline branching) ─────────────────────────────────────

    /// <summary>
    /// Adds a <c>$facet</c> stage, allowing multiple sub-pipelines to run in parallel.
    /// <para>key = facet name, value = sub-pipeline stages.</para>
    /// </summary>
    public MongoAggregateBuilder Facet(Dictionary<string, List<BsonDocument>> facets)
    {
        var facetDoc = new BsonDocument();
        foreach (var (name, pipeline) in facets)
            facetDoc.Add(name, new BsonArray(pipeline));
        _stages.Add(new BsonDocument("$facet", facetDoc));
        return this;
    }

    // ── Set / Merge ───────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a <c>$set</c> stage (alias for <c>$addFields</c> in Mongo 4.2+).
    /// </summary>
    public MongoAggregateBuilder Set(BsonDocument fields)
    {
        _stages.Add(new BsonDocument("$set", fields));
        return this;
    }

    /// <summary>Adds a <c>$count</c> stage that outputs a single doc with the count field.</summary>
    public MongoAggregateBuilder Count(string outputField = "total")
    {
        _stages.Add(new BsonDocument("$count", outputField));
        return this;
    }

    // ── Raw stage ─────────────────────────────────────────────────────────────

    /// <summary>Appends any raw BSON stage document directly.</summary>
    public MongoAggregateBuilder AddRawStage(BsonDocument rawStage)
    {
        _stages.Add(rawStage);
        return this;
    }

    /// <summary>Appends any raw JSON stage string directly.</summary>
    public MongoAggregateBuilder AddRawStage(string rawStageJson)
        => AddRawStage(BsonDocument.Parse(rawStageJson));

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>Returns an immutable copy of the built pipeline stages.</summary>
    public IReadOnlyList<BsonDocument> Build() => _stages.AsReadOnly();

    /// <summary>Converts to <see cref="PipelineDefinition{TInput,TOutput}"/> for use with the driver.</summary>
    public PipelineDefinition<BsonDocument, BsonDocument> ToPipelineDefinition()
        => PipelineDefinition<BsonDocument, BsonDocument>.Create(_stages);

    /// <summary>Returns the number of stages currently in the pipeline.</summary>
    public int StageCount => _stages.Count;
}
