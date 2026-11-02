using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Xunit;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class LegacyPaymentRulesTests
{
    [Theory]
    [InlineData("RRL1234567")]
    [InlineData("VSL1234567")]
    [InlineData("VVS1234567")]
    [InlineData("rrl1234567")]
    [InlineData("vSl1234567")]
    // These combinations are accepted by the executable character classes,
    // despite not appearing among the three prefixes in legacy report views.
    [InlineData("RSS0000000")]
    [InlineData("VRL9999999")]
    public void Ppk_accepts_exact_native_character_classes_and_ascii_digits(string receipt)
        => Assert.True(LegacyPaymentRules.IsValidPpkReceipt(receipt));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RRL123456")]
    [InlineData("RRL12345678")]
    [InlineData("ARL1234567")]
    [InlineData("RXL1234567")]
    [InlineData("RRX1234567")]
    [InlineData("RRL123456A")]
    [InlineData(" RRL1234567")]
    [InlineData("RRL1234567 ")]
    [InlineData("RRL1234567\n")]
    [InlineData("RRL123456\n")]
    [InlineData("RR\n1234567")]
    [InlineData("RRL１２３４５６７")]
    [InlineData("RRL١٢٣٤٥٦٧")]
    [InlineData("ＲRL1234567")]
    [InlineData("RRL1234567RRL1234567")]
    public void Ppk_rejects_missing_malformed_non_ascii_or_embedded_matching_substrings(string? receipt)
        => Assert.False(LegacyPaymentRules.IsValidPpkReceipt(receipt));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(81423987)]
    [InlineData(834019474)]
    public void Recognizes_each_executable_native_payment_method(int methodId)
        => Assert.True(LegacyPaymentRules.IsSupportedMethod(methodId));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Unknown_reference_rows_do_not_acquire_payment_behavior_by_label(int methodId)
        => Assert.False(LegacyPaymentRules.IsSupportedMethod(methodId));

    [Fact]
    public void Receiver_admission_does_not_require_the_register_default_for_a_visible_allowed_employee()
        => Assert.True(LegacyPaymentRules.IsReceiverAllowed(
            visibleInAllowedDepartment: true, explicitlyApproved: false, selectedRegisterDefault: false));

    [Fact]
    public void An_explicit_historical_receiver_exception_does_not_depend_on_visible_picker_membership()
        => Assert.True(LegacyPaymentRules.IsReceiverAllowed(
            visibleInAllowedDepartment: false, explicitlyApproved: true, selectedRegisterDefault: false));

    [Fact]
    public void Hidden_receiver_requires_the_current_register_default_or_an_independent_exception()
    {
        Assert.True(LegacyPaymentRules.IsReceiverAllowed(
            visibleInAllowedDepartment: false, explicitlyApproved: false, selectedRegisterDefault: true));

        // The SQL caller must resolve this flag again against the current EKA.
        // After switching registers the previous hidden default has no durable
        // permission: false current-context membership must revoke admission.
        Assert.False(LegacyPaymentRules.IsReceiverAllowed(
            visibleInAllowedDepartment: false, explicitlyApproved: false, selectedRegisterDefault: false));
    }

    [Fact]
    public void Register_change_does_not_revoke_a_separately_approved_receiver()
        => Assert.True(LegacyPaymentRules.IsReceiverAllowed(
            visibleInAllowedDepartment: false, explicitlyApproved: true, selectedRegisterDefault: false));

    [Theory]
    [InlineData("0.001", false)]
    [InlineData("0.0001", false)]
    [InlineData("0.01", true)]
    [InlineData("0", false)]
    [InlineData("-0.01", false)]
    [InlineData("1.001", false)]
    [InlineData("922337203685477.58", true)]
    [InlineData("922337203685477.59", false)]
    public void Payment_amount_matches_decimal_two_place_storage_and_money_trigger_limit(string text, bool valid)
        => Assert.Equal(valid, LegacyPaymentRules.IsValidPositiveAmount(Amount(text)));

    [Fact]
    public void Paid_four_place_tail_is_preserved_at_smallmoney_boundary()
    {
        LegacyPaymentRules.ValidateBalanceAddition(0.01m, 214748.3547m, 0);
        Assert.Equal(0.01m, LegacyPaymentRules.MaximumAdditionalAmount(214748.3547m));
        var error = Assert.Throws<OrderEntryException>(() =>
            LegacyPaymentRules.ValidateBalanceAddition(0.01m, 214748.3548m, 0));
        Assert.Equal("PAYMENT_PAID_RANGE", error.Code);
        Assert.Equal(0, LegacyPaymentRules.MaximumAdditionalAmount(214748.3548m));
    }

    [Fact]
    public void Receiver_cash_four_place_tail_is_checked_before_money_overflow()
    {
        LegacyPaymentRules.ValidateBalanceAddition(0.01m, 0, 922337203685477.5707m);
        var error = Assert.Throws<OrderEntryException>(() =>
            LegacyPaymentRules.ValidateBalanceAddition(0.01m, 0, 922337203685477.5708m));
        Assert.Equal("PAYMENT_CASH_RANGE", error.Code);
    }

    [Fact]
    public void Fractional_cent_is_rejected_instead_of_rounding_into_a_posted_payment()
    {
        var error = Assert.Throws<OrderEntryException>(() =>
            LegacyPaymentRules.ValidateBalanceAddition(0.001m, 0, 0));
        Assert.Equal("PAYMENT_AMOUNT", error.Code);
    }

    private static decimal Amount(string text)
        => decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
}
