using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Tests.Infrastructure;

/// <summary>The ownership rule from design.md section 2, as a pure decision table.</summary>
[Trait("Category", "Unit")]
public sealed class AccountAccessTests
{
    private static readonly Caller Trader = new("trader-subject", IsOps: false);
    private static readonly Caller Ops = new("ops-subject", IsOps: true);

    private static Account OwnedBy(string subject) => new()
    {
        Code = "X",
        Name = "X",
        BaseCurrency = "USD",
        OwnerSubject = subject,
    };

    [Fact]
    public void A_trader_may_use_their_own_account()
        => AccountAccess.Decide(Trader, OwnedBy(Trader.Subject)).Should().Be(AccessDecision.Allowed);

    [Fact]
    public void A_trader_is_forbidden_someone_elses_account()
        => AccountAccess.Decide(Trader, OwnedBy("someone-else")).Should().Be(AccessDecision.Forbidden);

    [Fact]
    public void A_trader_is_forbidden_rather_than_told_an_account_does_not_exist()
        => AccountAccess.Decide(Trader, account: null).Should().Be(AccessDecision.Forbidden);

    [Fact]
    public void Ops_may_use_any_account()
        => AccountAccess.Decide(Ops, OwnedBy("anyone")).Should().Be(AccessDecision.Allowed);

    [Fact]
    public void Ops_is_told_when_an_account_does_not_exist()
        => AccountAccess.Decide(Ops, account: null).Should().Be(AccessDecision.NotFound);
}
