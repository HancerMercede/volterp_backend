using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CompanyCertificateRepository(VolterpDbContext context)
    : RepositoryBase<CompanyCertificate>(context), ICompanyCertificateRepository
{
}
