using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Sales.WebUI.Resources;

/// <summary>Presentation-only translations of known API text. Legacy data and unknown errors remain verbatim.</summary>
public static class SalesApiErrors
{
    private static readonly IReadOnlyDictionary<string,string> MessageKeys = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["The request could not be completed. Try again."] = "Errors.Api.Generic",
        ["One or more validation errors occurred."] = "Errors.Api.RequestValidation",
        ["Unauthorized"] = "Errors.Api.Unauthorized",
        ["Forbidden"] = "Errors.Api.Forbidden",
        ["Service Unavailable"] = "Errors.Api.ServiceUnavailable",
        ["Trusted Portal employee identity required."] = "Errors.Api.InvalidActor",
        ["This deployment has no financial/confirmation adapter."] = "Errors.Api.FinancialDisabled",
        ["Sign in to use Sales."] = "Errors.Api.AuthenticatedActorRequired",
        ["Your active Cikada account needs a legacy SQL login mapping. Contact an administrator."] = "Errors.Api.LegacyMappingRequired",
        ["The user access service is unavailable. Try again."] = "Errors.Api.IdentityServiceUnavailable",
        ["Order or product not found."] = "Errors.Api.NotFound",
        ["Your account has no valid legacy SQL access mapping."] = "Errors.Api.LegacySqlIdentityRequired",
        ["The legacy SQL connection has an unexpected security context."] = "Errors.Api.LegacySqlContextInvalid",
        ["Your mapped legacy SQL login is unavailable or disabled."] = "Errors.Api.LegacySqlLoginUnavailable",
        ["Your account is not permitted to use the SQL service login."] = "Errors.Api.LegacySqlServiceLoginForbidden",
        ["The legacy SQL identity could not be established."] = "Errors.Api.LegacySqlContextInvalid2",
        ["Address is required."] = "Errors.Api.AddressRequired",
        ["An address field exceeds its legacy limit."] = "Errors.Api.AddressLength",
        ["Duty is required."] = "Errors.Api.DutyRequired",
        ["Duty date exceeds legacy smalldatetime storage."] = "Errors.Api.DutyDate",
        ["Duty note is limited to 512 characters."] = "Errors.Api.DutyNote",
        ["This duty assignment is outside the editable acquisition period."] = "Errors.Api.DutyPosted",
        ["Command identity is required."] = "Errors.Api.InvalidCommand",
        ["Order not found."] = "Errors.Api.NotFound2",
        ["Sales saving is currently disabled."] = "Errors.Api.WritesDisabled",
        ["An authenticated actor and command ID are required."] = "Errors.Api.InvalidCommand2",
        ["Sales test-write scope is not configured."] = "Errors.Api.WriteConfiguration",
        ["Sales saving is disabled for this database."] = "Errors.Api.DatabaseMismatch",
        ["This command ID was already used for different input."] = "Errors.Api.CommandIdReused",
        ["Order changed in another session. Reload before saving."] = "Errors.Api.StaleOrder",
        ["A record with these identifiers already exists."] = "Errors.Api.Duplicate",
        ["Saving is restricted to the configured Testavimas series."] = "Errors.Api.TestBranchOnly",
        ["This order cannot be edited in its current state."] = "Errors.Api.OrderLocked",
        ["Product does not belong to this order."] = "Errors.Api.NotFound3",
        ["Product is locked in its current state."] = "Errors.Api.ProductLocked",
        ["Search is limited to150 characters."] = "Errors.Api.PaymentSearch",
        ["Payment choices are restricted to configured Testavimas orders."] = "Errors.Api.TestBranchOnly2",
        ["Payment posting is disabled for this deployment."] = "Errors.Api.PaymentsDisabled",
        ["Production confirmation is not enabled for this deployment."] = "Errors.Api.ConfirmationDisabled",
        ["Cancelled orders cannot be confirmed."] = "Errors.Api.OrderCancelled",
        ["Select one to five hundred distinct products from this order."] = "Errors.Api.ConfirmationItems",
        ["Customer confirmation policy could not be established."] = "Errors.Api.EligibilityUnavailable",
        ["Customer confirmation action is not supported."] = "Errors.Api.InvalidEligibilityAction",
        ["Selected product is outside this order."] = "Errors.Api.NotFound4",
        ["Product type is required before confirmation."] = "Errors.Api.IncompleteProduct",
        ["Stored product type or category is unavailable."] = "Errors.Api.TypeUnavailable",
        ["Product has no production barcode."] = "Errors.Api.BarcodeRequired",
        ["The legacy confirmation stock rule blocks a selected product."] = "Errors.Api.ConfirmationStock",
        ["The production-document column no longer supports the verified number format."] = "Errors.Api.DocumentNumberSchema",
        ["Product configuration is required."] = "Errors.Api.ProductRequired",
        ["Cancelled orders cannot be configured in Sales."] = "Errors.Api.OrderCancelled2",
        ["Review the current server calculation before saving."] = "Errors.Api.CalculationPreviewRequired",
        ["Calculation or price references changed. Review a fresh calculation before saving."] = "Errors.Api.CalculationChanged",
        ["Remove attached accessories before changing the product type."] = "Errors.Api.TypeAccessories",
        ["Manual PPP slot indices must be distinct values from zero to eleven."] = "Errors.Api.PppOverrideShape",
        ["A hidden technical parameter cannot be overridden in this editor."] = "Errors.Api.PppHidden",
        ["Order date is required."] = "Errors.Api.DateRequired",
        ["List price changed. Look up the price again before saving."] = "Errors.Api.PriceChanged",
        ["Product not saved: this product requires a price from the price list. Click From price list; if no price is found, check the material, dimensions and order date."] = "Errors.Api.PriceListRequired",
        ["Type or category is not active."] = "Errors.Api.TypeUnavailable2",
        ["Series not found."] = "Errors.Api.NotFound5",
        ["Choose a configured Testavimas series."] = "Errors.Api.TestBranchOnly3",
        ["Persisted order series and number cannot be changed through this editor."] = "Errors.Api.NumberImmutable",
        ["Order header is required."] = "Errors.Api.HeaderRequired",
        ["Enter a valid SQL order date."] = "Errors.Api.InvalidDate",
        ["Order note is limited to 128 characters."] = "Errors.Api.NoteLength",
        ["Discount exceeds legacy decimal precision."] = "Errors.Api.DiscountRange",
        ["This discount requires a separately verified mail/operational path."] = "Errors.Api.DiscountNotEnabled",
        ["Customer does not exist."] = "Errors.Api.InvalidCustomer",
        ["Customer overdue invoices block product entry."] = "Errors.Api.CustomerOverdue",
        ["The customer has an unsupported eligibility action. Select a customer with a verified policy."] = "Errors.Api.InvalidEligibilityAction2",
        ["Only entered or stopped orders can be cancelled."] = "Errors.Api.CancellationState",
        ["Legacy cancellation audit storage is not compatible with the reviewed schema."] = "Errors.Api.CancellationAuditConfiguration",
        ["Duty assignment was not found."] = "Errors.Api.NotFound6",
        ["Payment posting is not enabled for this deployment."] = "Errors.Api.PaymentsDisabled2",
        ["Payment input is required."] = "Errors.Api.PaymentRequired",
        ["Payments cannot be posted to a cancelled order through this test flow."] = "Errors.Api.OrderCancelled3",
        ["Verified payment currency must be configured."] = "Errors.Api.PaymentConfiguration",
        ["Choose an existing supported legacy payment method."] = "Errors.Api.PaymentMethod",
        ["Enter a positive amount with two decimal places, matching legacy payment storage; refunds are not enabled."] = "Errors.Api.PaymentAmount",
        ["Payment note is limited to 500 characters."] = "Errors.Api.PaymentNote",
        ["Only the current SQL business date is enabled for contained test postings."] = "Errors.Api.PaymentDate",
        ["The configured new-payment currency must match the order currency. No conversion of legacy amounts is performed."] = "Errors.Api.PaymentCurrencyMismatch",
        ["Enter one valid ten-character PPK receipt and no bank/register reference."] = "Errors.Api.PpkReference",
        ["Choose a bank transfer and clear other methods' document references."] = "Errors.Api.TransferScope",
        ["Choose an existing Lithuania bank transfer."] = "Errors.Api.TransferUnavailable",
        ["Bank transfer does not have enough unassigned amount."] = "Errors.Api.TransferAvailable",
        ["The bank transfer date is outside the current SQL account's posting period."] = "Errors.Api.TransferPeriodClosed",
        ["Cash posting must not carry another method's document reference."] = "Errors.Api.CashReference",
        ["Choose a register and clear other methods' document references."] = "Errors.Api.RegisterScope",
        ["Choose an existing cash register from an accessible department."] = "Errors.Api.RegisterUnavailable",
        ["This legacy payment method is not implemented."] = "Errors.Api.PaymentMethod2",
        ["Choose a visible accessible receiver, an explicitly approved receiver, or this selected register's actual default."] = "Errors.Api.PaymentReceiverScope",
        ["The current SQL account's payment work period does not include this date. No payment has been posted."] = "Errors.Api.PeriodClosed",
        ["Apply the reviewed additive receipt schema before enabling this operation."] = "Errors.Api.ReceiptSchema",
        ["Enter a positive accessory quantity with up to three decimal places."] = "Errors.Api.AccessoryQuantity",
        ["Accessory is not applicable to this product type."] = "Errors.Api.AccessoryUnavailable",
        ["Accessory is not attached to this product."] = "Errors.Api.NotFound7",
        ["This product cannot be removed."] = "Errors.Api.ProductLocked2",
        ["A product with production history cannot be removed through Sales."] = "Errors.Api.ProductionHistory",
        ["Complete the source product before duplicating it."] = "Errors.Api.IncompleteProduct2",
        ["Complete attached accessories before duplicating this product."] = "Errors.Api.IncompleteAccessory",
        ["A source accessory is no longer applicable; duplication was not saved."] = "Errors.Api.AccessoryUnavailable2",
        ["Configure a verified one-to-five-character Sales document prefix and unique identifier."] = "Errors.Api.ConfirmationConfiguration",
        ["Production document number exceeds the verified downstream legacy contract."] = "Errors.Api.DocumentNumberLength",
        ["Complete and save the product configuration before confirmation."] = "Errors.Api.IncompleteProduct3",
        ["Stored product type metadata does not match."] = "Errors.Api.TypeUnavailable3",
        ["Only entered or stopped products can be confirmed."] = "Errors.Api.ConfirmationState",
        ["Select manufactured product lines for confirmation."] = "Errors.Api.NotManufactured",
        ["Establish the fixed product price from the price list before confirmation."] = "Errors.Api.PriceListRequired2",
        ["Check dimensions and positive quantity (up to three decimal places)."] = "Errors.Api.InvalidDimensions",
        ["The pricing unit requires positive product dimensions."] = "Errors.Api.DimensionsRequired",
        ["Legacy control value is limited to ten characters."] = "Errors.Api.ControlLength",
        ["Product notes are limited to 2048 characters."] = "Errors.Api.ProductNoteLength",
        ["Twelve parameter slots are required."] = "Errors.Api.PppShape",
        ["Price must be nonnegative and use up to four decimal places."] = "Errors.Api.InvalidPrice",
        ["Enter a positive amount with two decimal places, matching legacy payment storage."] = "Errors.Api.PaymentAmount2",
        ["The resulting Paid amount would exceed legacy SMALLMONEY storage."] = "Errors.Api.PaymentPaidRange",
        ["The resulting receiver cash amount is outside legacy MONEY storage."] = "Errors.Api.PaymentCashRange",
        ["Order number and live series are required."] = "Errors.Api.InvalidNumber",
        ["Number must use the selected live order series."] = "Errors.Api.NumberSeries",
        ["Enter a positive order number with an optional group suffix."] = "Errors.Api.InvalidNumber2",
        ["The current-year suffix permits at most two additional characters."] = "Errors.Api.InvalidGroup",
        ["Enter a nonnegative final price with up to four decimal places."] = "Errors.Api.InvalidFinalPrice",
        ["No discountable amount exists for the requested final price."] = "Errors.Api.NoDiscountableAmount",
        ["Requested final price exceeds the legacy discount range."] = "Errors.Api.FinalPriceRange",
        ["Your legacy SQL account does not permit this operation."] = "Errors.Api.YourlegacySQLaccountdoesnotpermit",
        ["Concurrent database operation was rolled back. Retry the same command."] = "Errors.Api.ConcurrentdatabaseoperationwasrolledbackRetry",
        ["The database could not load the requested data. Try again."] = "Errors.Api.Thedatabasecouldnotloadtherequested",
        ["The database did not confirm the save. Your input is retained; use Check / retry save."] = "Errors.Api.Thedatabasedidnotconfirmthesave",
        ["Invalid reference query"] = "Errors.Api.Invalidreferencequery",
        ["Invalid address query"] = "Errors.Api.Invalidaddressquery",
        ["Invalid unit-price query"] = "Errors.Api.Invalidunitpricequery",
        ["Order cancellation is disabled for this deployment. The legacy audit bigint compatibility issue is deferred by the owner."] = "Errors.Api.Ordercancellationisdisabledforthisdeployment",
        ["Cancellation is unavailable until the legacy audit OrderID column supports bigint order IDs. The order has not been cancelled."] = "Errors.Api.Cancellationisunavailableuntilthelegacyaudit",
        ["Customer policy prevents production confirmation."] = "Errors.Api.Customerpolicypreventsproductionconfirmation",
        ["Product not saved: the price list contains conflicting or incomplete prices for this configuration. Ask the price-list administrator to correct the price before saving."] = "Errors.Api.Productnotsavedthepricelistcontains",
        ["Exactly twelve PPP formula slots are required."] = "Errors.Api.ExactlytwelvePPPformulaslotsarerequired",
        ["Exactly twelve PPP slots are required."] = "Errors.Api.ExactlytwelvePPPslotsarerequired",
        ["The nera text literal is not valid for numeric material consumption."] = "Errors.Api.Theneratextliteralisnotvalid",
        ["The formula did not produce a finite value."] = "Errors.Api.Theformuladidnotproduceafinite",
        ["The PPP value exceeds fifteen characters."] = "Errors.Api.ThePPPvalueexceedsfifteencharacters",
        ["PPP12 must be a decimal number."] = "Errors.Api.PPP12mustbeadecimalnumber",
        ["PPP12 exceeds legacy storage precision."] = "Errors.Api.PPP12exceedslegacystorageprecision",
        ["Material requirement must be nonnegative."] = "Errors.Api.Materialrequirementmustbenonnegative",
        ["The material shortage action is not configured."] = "Errors.Api.Thematerialshortageactionisnotconfigured",
        ["Select a material for positive consumption or a list-price lookup."] = "Errors.Api.Selectamaterialforpositiveconsumptionor",
        ["Unsupported formula syntax."] = "Errors.Api.Unsupportedformulasyntax",
        ["Formula division by zero."] = "Errors.Api.Formuladivisionbyzero",
        ["Missing formula parenthesis."] = "Errors.Api.Missingformulaparenthesis",
        ["Missing formula operand."] = "Errors.Api.Missingformulaoperand",
        ["Colour is required by this formula."] = "Errors.Api.Colourisrequiredbythisformula",
        ["This formula requires a numeric control value and colour."] = "Errors.Api.Thisformularequiresanumericcontrolvalue",
        ["Round precision is required."] = "Errors.Api.Roundprecisionisrequired",
        ["Invalid Round arguments."] = "Errors.Api.InvalidRoundarguments",
        ["Invalid Round precision."] = "Errors.Api.InvalidRoundprecision",
        ["Invalid function arguments."] = "Errors.Api.Invalidfunctionarguments",
        ["Unsupported formula operand."] = "Errors.Api.Unsupportedformulaoperand",
        ["Calculation step is zero."] = "Errors.Api.Calculationstepiszero",
        ["One carrier cannot produce a corrector spacing."] = "Errors.Api.Onecarriercannotproduceacorrectorspacing",
        ["Customer reference is unavailable."] = "Errors.Api.Customerreferenceisunavailable",
        ["Store reference is unavailable."] = "Errors.Api.Storereferenceisunavailable",
        ["Manager reference is unavailable."] = "Errors.Api.Managerreferenceisunavailable",
        ["Consultant reference is unavailable."] = "Errors.Api.Consultantreferenceisunavailable",
        ["Priority reference is unavailable."] = "Errors.Api.Priorityreferenceisunavailable",
        ["Employee reference is unavailable."] = "Errors.Api.Employeereferenceisunavailable",
        ["Duty reference is unavailable."] = "Errors.Api.Dutyreferenceisunavailable",
        ["Street reference is unavailable."] = "Errors.Api.Streetreferenceisunavailable",
    };

    private static readonly IReadOnlyDictionary<string,string> CodeKeys = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["ACCESSORY_QUANTITY"] = "Errors.Api.AccessoryQuantity",
        ["ADDRESS_LENGTH"] = "Errors.Api.AddressLength",
        ["ADDRESS_REQUIRED"] = "Errors.Api.AddressRequired",
        ["AUTHENTICATED_ACTOR_REQUIRED"] = "Errors.Api.AuthenticatedActorRequired",
        ["BARCODE_REQUIRED"] = "Errors.Api.BarcodeRequired",
        ["CALCULATION_CHANGED"] = "Errors.Api.CalculationChanged",
        ["CALCULATION_PREVIEW_REQUIRED"] = "Errors.Api.CalculationPreviewRequired",
        ["CANCELLATION_AUDIT_CONFIGURATION"] = "Errors.Api.CancellationAuditConfiguration",
        ["CANCELLATION_STATE"] = "Errors.Api.CancellationState",
        ["CASH_REFERENCE"] = "Errors.Api.CashReference",
        ["COMMAND_ID_REUSED"] = "Errors.Api.CommandIdReused",
        ["CONFIRMATION_CONFIGURATION"] = "Errors.Api.ConfirmationConfiguration",
        ["CONFIRMATION_DISABLED"] = "Errors.Api.ConfirmationDisabled",
        ["CONFIRMATION_ITEMS"] = "Errors.Api.ConfirmationItems",
        ["CONFIRMATION_STATE"] = "Errors.Api.ConfirmationState",
        ["CONFIRMATION_STOCK"] = "Errors.Api.ConfirmationStock",
        ["CONTROL_LENGTH"] = "Errors.Api.ControlLength",
        ["CUSTOMER_OVERDUE"] = "Errors.Api.CustomerOverdue",
        ["DATABASE_MISMATCH"] = "Errors.Api.DatabaseMismatch",
        ["DATE_REQUIRED"] = "Errors.Api.DateRequired",
        ["DIMENSIONS_REQUIRED"] = "Errors.Api.DimensionsRequired",
        ["DISCOUNT_NOT_ENABLED"] = "Errors.Api.DiscountNotEnabled",
        ["DISCOUNT_RANGE"] = "Errors.Api.DiscountRange",
        ["DOCUMENT_NUMBER_LENGTH"] = "Errors.Api.DocumentNumberLength",
        ["DOCUMENT_NUMBER_SCHEMA"] = "Errors.Api.DocumentNumberSchema",
        ["DUPLICATE"] = "Errors.Api.Duplicate",
        ["DUTY_DATE"] = "Errors.Api.DutyDate",
        ["DUTY_NOTE"] = "Errors.Api.DutyNote",
        ["DUTY_POSTED"] = "Errors.Api.DutyPosted",
        ["DUTY_REQUIRED"] = "Errors.Api.DutyRequired",
        ["ELIGIBILITY_UNAVAILABLE"] = "Errors.Api.EligibilityUnavailable",
        ["FINAL_PRICE_RANGE"] = "Errors.Api.FinalPriceRange",
        ["FINANCIAL_DISABLED"] = "Errors.Api.FinancialDisabled",
        ["HEADER_REQUIRED"] = "Errors.Api.HeaderRequired",
        ["IDENTITY_SERVICE_UNAVAILABLE"] = "Errors.Api.IdentityServiceUnavailable",
        ["INCOMPLETE_ACCESSORY"] = "Errors.Api.IncompleteAccessory",
        ["INVALID_ACTOR"] = "Errors.Api.InvalidActor",
        ["INVALID_CUSTOMER"] = "Errors.Api.InvalidCustomer",
        ["INVALID_DATE"] = "Errors.Api.InvalidDate",
        ["INVALID_DIMENSIONS"] = "Errors.Api.InvalidDimensions",
        ["INVALID_FINAL_PRICE"] = "Errors.Api.InvalidFinalPrice",
        ["INVALID_GROUP"] = "Errors.Api.InvalidGroup",
        ["INVALID_PRICE"] = "Errors.Api.InvalidPrice",
        ["LEGACY_MAPPING_REQUIRED"] = "Errors.Api.LegacyMappingRequired",
        ["LEGACY_SQL_IDENTITY_REQUIRED"] = "Errors.Api.LegacySqlIdentityRequired",
        ["LEGACY_SQL_LOGIN_UNAVAILABLE"] = "Errors.Api.LegacySqlLoginUnavailable",
        ["LEGACY_SQL_SERVICE_LOGIN_FORBIDDEN"] = "Errors.Api.LegacySqlServiceLoginForbidden",
        ["NOTE_LENGTH"] = "Errors.Api.NoteLength",
        ["NOT_MANUFACTURED"] = "Errors.Api.NotManufactured",
        ["NO_DISCOUNTABLE_AMOUNT"] = "Errors.Api.NoDiscountableAmount",
        ["NUMBER_IMMUTABLE"] = "Errors.Api.NumberImmutable",
        ["NUMBER_SERIES"] = "Errors.Api.NumberSeries",
        ["ORDER_LOCKED"] = "Errors.Api.OrderLocked",
        ["PAYMENT_CASH_RANGE"] = "Errors.Api.PaymentCashRange",
        ["PAYMENT_CONFIGURATION"] = "Errors.Api.PaymentConfiguration",
        ["PAYMENT_CURRENCY_MISMATCH"] = "Errors.Api.PaymentCurrencyMismatch",
        ["PAYMENT_DATE"] = "Errors.Api.PaymentDate",
        ["PAYMENT_NOTE"] = "Errors.Api.PaymentNote",
        ["PAYMENT_PAID_RANGE"] = "Errors.Api.PaymentPaidRange",
        ["PAYMENT_RECEIVER_SCOPE"] = "Errors.Api.PaymentReceiverScope",
        ["PAYMENT_REQUIRED"] = "Errors.Api.PaymentRequired",
        ["PAYMENT_SEARCH"] = "Errors.Api.PaymentSearch",
        ["PERIOD_CLOSED"] = "Errors.Api.PeriodClosed",
        ["PPK_REFERENCE"] = "Errors.Api.PpkReference",
        ["PPP_HIDDEN"] = "Errors.Api.PppHidden",
        ["PPP_OVERRIDE_SHAPE"] = "Errors.Api.PppOverrideShape",
        ["PPP_SHAPE"] = "Errors.Api.PppShape",
        ["PRICE_CHANGED"] = "Errors.Api.PriceChanged",
        ["PRODUCTION_HISTORY"] = "Errors.Api.ProductionHistory",
        ["PRODUCT_NOTE_LENGTH"] = "Errors.Api.ProductNoteLength",
        ["PRODUCT_REQUIRED"] = "Errors.Api.ProductRequired",
        ["RECEIPT_SCHEMA"] = "Errors.Api.ReceiptSchema",
        ["REGISTER_SCOPE"] = "Errors.Api.RegisterScope",
        ["REGISTER_UNAVAILABLE"] = "Errors.Api.RegisterUnavailable",
        ["STALE_ORDER"] = "Errors.Api.StaleOrder",
        ["TRANSFER_AVAILABLE"] = "Errors.Api.TransferAvailable",
        ["TRANSFER_PERIOD_CLOSED"] = "Errors.Api.TransferPeriodClosed",
        ["TRANSFER_SCOPE"] = "Errors.Api.TransferScope",
        ["TRANSFER_UNAVAILABLE"] = "Errors.Api.TransferUnavailable",
        ["TYPE_ACCESSORIES"] = "Errors.Api.TypeAccessories",
        ["WRITES_DISABLED"] = "Errors.Api.WritesDisabled",
        ["WRITE_CONFIGURATION"] = "Errors.Api.WriteConfiguration",
    };

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);
    private static readonly Regex FieldPrefix = Pattern(@"\A(?<field>widthMm|heightMm|quantity|colourId|control|unitPrice|note|parameters|PPP[0-9]{2}): (?<text>[\s\S]*)\z");
    private static readonly Regex PriceMissing = Pattern(@"\AProduct not saved: no list price matches this configuration \((?<width>[0-9]+) × (?<height>[0-9]+) mm; order date (?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})\)\. Check the material and dimensions, or ask the price-list administrator to add a price\.\z");
    private static readonly Regex Stock = Pattern(@"\A(?<blocking>Product not saved: insufficient material\.|Material shortage\.) Required (?<required>-?[0-9]+(?:\.[0-9]+)?)(?<requiredUnit> [^;\r\n]*)?; available (?<available>-?[0-9]+(?:\.[0-9]+)?)(?<availableUnit> [^\r\n]*?)?\.(?<equality> The legacy rule requires availability greater than the required quantity\.)? (?<ending>Choose another material or reduce the quantity or dimensions\.|Entry is allowed with a warning; confirm availability with the responsible employee\.)\z");
    private static readonly Regex FormulaValue = Pattern(@"\A(?<kind>Unsupported formula token|Unsupported formula function|Missing calculation constant): (?<value>[A-Za-z_][A-Za-z0-9_]*)\.\z");

    public static string Translate(IStringLocalizer<SalesResources> l, string code, string message)
    {
        ArgumentNullException.ThrowIfNull(l);
        if (string.IsNullOrWhiteSpace(message))
        {
            var key = code is not null && CodeKeys.TryGetValue(code, out var known) ? known : "Errors.Api.Generic";
            var localized = l[key];
            return localized.ResourceNotFound ? "The request could not be completed. Try again." : localized.Value;
        }
        try
        {
            // Preserve field identifiers used by error focusing; translate only the recognized description.
            var prefix = FieldPrefix.Match(message);
            if (prefix.Success)
                return prefix.Groups["field"].Value + ": " + TranslateText(l, prefix.Groups["text"].Value);
            return TranslateText(l, message);
        }
        catch (RegexMatchTimeoutException)
        {
            return message;
        }
    }

    private static string TranslateText(IStringLocalizer<SalesResources> l, string message)
    {
        if (MessageKeys.TryGetValue(message, out var key)) return Localize(l, key, message);
        var price = PriceMissing.Match(message);
        if (price.Success)
            return Localize(l, "Errors.Api.PriceMissing", message,
                price.Groups["width"].Value, price.Groups["height"].Value, price.Groups["date"].Value);
        var stock = Stock.Match(message);
        if (stock.Success)
        {
            var blocking = stock.Groups["blocking"].Value.StartsWith("Product", StringComparison.Ordinal);
            // Reject mixed sentence shapes; unknown or database-provided prose must remain unchanged.
            var blockingEnding = stock.Groups["ending"].Value.StartsWith("Choose", StringComparison.Ordinal);
            if (blocking != blockingEnding) return message;
            key = "Errors.Api.Stock" + (blocking ? "Blocking" : "Warning")
                + (stock.Groups["equality"].Success ? "ExactBalance" : "");
            return Localize(l, key, message, stock.Groups["required"].Value, stock.Groups["requiredUnit"].Value,
                stock.Groups["available"].Value, stock.Groups["availableUnit"].Value);
        }
        var formula = FormulaValue.Match(message);
        if (formula.Success)
        {
            key = formula.Groups["kind"].Value switch
            {
                "Unsupported formula token" => "Errors.Api.UnsupportedFormulaToken",
                "Unsupported formula function" => "Errors.Api.UnsupportedFormulaFunction",
                _ => "Errors.Api.MissingCalculationConstant"
            };
            return Localize(l, key, message, formula.Groups["value"].Value);
        }
        return message;
    }

    private static string Localize(IStringLocalizer<SalesResources> l, string key, string original, params object[] arguments)
    {
        var translated = arguments.Length == 0 ? l[key] : l[key, arguments];
        return translated.ResourceNotFound ? original : translated.Value;
    }

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant, MatchTimeout);
}
