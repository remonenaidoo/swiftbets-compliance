using SwiftBets.Compliance.Domain.Audit;

namespace SwiftBets.Compliance.Domain.Tests;

public sealed class AuditChainTests
{
    private static AuditRecord Record(string action, string? before = null) =>
        new(Guid.NewGuid(), "compliance", "self", action, "user", "u-1", before, """{"amount":1}""", "corr", new DateTimeOffset(2026, 10, 1, 12, 0, 0, 123, TimeSpan.FromHours(2)).AddTicks(4567));

    private static List<ChainedAuditRecord> Chain(params AuditRecord[] records)
    {
        var previous = AuditChain.Genesis;
        var chain = new List<ChainedAuditRecord>();
        foreach (var (record, i) in records.Select((r, i) => (r, i)))
        {
            var hash = AuditChain.HashOf(previous, record);
            chain.Add(new ChainedAuditRecord(i + 1, record.Normalised(), previous, hash));
            previous = hash;
        }

        return chain;
    }

    [Fact]
    public void Hash_ignores_sub_millisecond_ticks_and_the_offset_but_not_null_versus_empty()
    {
        var record = Record("limit.set");

        AuditChain.HashOf(AuditChain.Genesis, record).ShouldBe(AuditChain.HashOf(AuditChain.Genesis, record.Normalised()));
        AuditChain.HashOf(AuditChain.Genesis, record with { Before = "" }).ShouldNotBe(AuditChain.HashOf(AuditChain.Genesis, record));
    }

    [Fact]
    public void An_intact_chain_has_no_break()
    {
        var chain = Chain(Record("a"), Record("b"), Record("c"));

        AuditChain.FirstBreak(AuditChain.Genesis, chain, out var last).ShouldBeNull();
        last.ShouldBe(chain[^1].Hash);
    }

    [Fact]
    public void Changing_an_entry_breaks_the_chain_at_that_entry()
    {
        var chain = Chain(Record("a"), Record("b"), Record("c"));
        chain[1] = chain[1] with { Record = chain[1].Record with { Actor = "someone else" } };

        AuditChain.FirstBreak(AuditChain.Genesis, chain, out _).ShouldBe(2);
    }

    [Fact]
    public void Removing_an_entry_breaks_the_chain_at_the_next_one()
    {
        var chain = Chain(Record("a"), Record("b"), Record("c"));
        chain.RemoveAt(1);

        AuditChain.FirstBreak(AuditChain.Genesis, chain, out _).ShouldBe(3);
    }
}
