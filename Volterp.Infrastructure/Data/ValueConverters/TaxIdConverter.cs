using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Volterp.Domain.ValueObjects;

namespace Volterp.Infrastructure.Data.ValueConverters;

public class TaxIdConverter : ValueConverter<TaxId, string>
{
    public TaxIdConverter()
        : base(
            taxId => taxId.Value,
            value => new TaxId(value))
    {
    }
}
