using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public static class CodeFormat
{
    private static readonly IReadOnlyDictionary<CodeEntityType, string> Prefixes = new Dictionary<CodeEntityType, string>
    {
        [CodeEntityType.Product] = "Prod",
        [CodeEntityType.Category] = "Cat",
        [CodeEntityType.Client] = "Cli",
        [CodeEntityType.Supplier] = "Prov",
        [CodeEntityType.Sale] = "Vent",
        [CodeEntityType.Purchase] = "Comp",
        [CodeEntityType.Employee] = "Empl",
        [CodeEntityType.AccountingTransaction] = "Asi",
        [CodeEntityType.User] = "Usu"
    };

    public static string Build(CodeEntityType entityType, int value)
        => $"{Prefixes[entityType]}-{value:000}";
}
