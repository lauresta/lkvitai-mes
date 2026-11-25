namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>Display rules for actors, statuses and location roles on the item card.</summary>
public static class ItemCardText
{
    /// <summary>
    /// True for actors written by the system: empty, "system", or a long hex hash without dashes
    /// (e.g. the SHA-256 written by the Excel import). GUID user ids are not treated as the system.
    /// </summary>
    public static bool IsSystemActor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return true;
        }

        var value = actor.Trim();
        return string.Equals(value, "system", StringComparison.OrdinalIgnoreCase)
               || (value.Length >= 32 && value.All(char.IsAsciiHexDigit));
    }

    /// <summary>The actor's name, <paramref name="systemLabel"/> for system actors, a short tail for other opaque ids.</summary>
    public static string Actor(string? actor, string systemLabel, Func<string, string> unknownUser)
    {
        if (IsSystemActor(actor))
        {
            return systemLabel;
        }

        return ActorLabel.Display(actor, unknownUser);
    }

    /// <summary>Counted-noun resource key: "{baseKey}.One|Few|Many|Other" for the UI language.</summary>
    public static string PluralKey(string baseKey, int count, string? language)
        => $"{baseKey}.{PluralForm.Suffix(count, language)}";

    public static string PluralKey(string baseKey, int count)
        => PluralKey(baseKey, count, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    /// <summary>Resource key of a location role label, e.g. "AdminItemDetail.Role.Receiving".</summary>
    public static string? RoleKey(string? role)
        => string.IsNullOrWhiteSpace(role) ? null : "AdminItemDetail.Role." + role.Trim();

    /// <summary>Chip of a reservation or sales order status: picking / packed = warn, allocated = info, others slate.</summary>
    public static string ReservationChipClass(string? status)
        => Normalize(status) switch
        {
            "PICKING" or "PACKED" => "chip chip--low",
            "ALLOCATED" or "PENDINGSTOCK" => "chip chip--reserved",
            _ => "chip chip--locked"
        };

    /// <summary>Chip of an inbound shipment status: partly received = info, not started = slate.</summary>
    public static string IncomingChipClass(string? status)
        => Normalize(status) == "PARTIAL" ? "chip chip--reserved" : "chip chip--locked";

    /// <summary>Resource key of the item type label: "Sandėliuojama" for stock items, "Paslauga" for services.</summary>
    public static string ItemTypeKey(string? itemType)
        => string.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase)
            ? "AdminItemDetail.ItemType.Service"
            : "AdminItemDetail.ItemType.Stock";

    /// <summary>Item status chip (same mapping as the item list).</summary>
    public static string StatusChipClass(string? status)
        => status switch
        {
            "Active" => "chip chip--ok",
            "Obsolete" => "chip chip--low",
            "Discontinued" => "chip chip--locked",
            _ => "chip chip--reserved"
        };

    private static string Normalize(string? status)
        => (status ?? string.Empty).Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
