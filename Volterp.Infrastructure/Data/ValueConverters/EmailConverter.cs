using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Volterp.Domain.ValueObjects;

namespace Volterp.Infrastructure.Data.ValueConverters;

public class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(
            email => email.Value,
            value => new Email(value))
    {
    }
}
