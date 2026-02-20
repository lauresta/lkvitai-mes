using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "SalesOrders")]
public sealed class CustomerTests
{
    [Fact]
    public void NewCustomer_Defaults_ShouldMatchExpectedState()
    {
        var customer = new Customer
        {
            Name = "Acme Industries",
            Email = "ops@acme.test",
            BillingAddress = new Address
            {
                Street = "Main 1",
                City = "Vilnius",
                State = "LT",
                ZipCode = "10000",
                Country = "LT"
            }
        };

        customer.Status.Should().Be(CustomerStatus.Active);
        customer.PaymentTerms.Should().Be(PaymentTerms.Net30);
        customer.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void CreditLimit_CanBeNull_ForUnlimitedCredit()
    {
        var customer = new Customer
        {
            Name = "NoLimit",
            Email = "noreply@test.local",
            BillingAddress = new Address()
        };

        customer.CreditLimit.Should().BeNull();
    }
}
