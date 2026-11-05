using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Services;

public class ClientServiceTests
{
    [Fact]
    public async Task CreateClientAsync_WithInvalidTaxId_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: "123"
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Left>();
        var error = ((Either<Error, ClientDto>.Left)result).Value;
        error.Message.Should().Be("The tax id must be a valid RNC or citizen ID.");
        mockClientsRepo.Verify(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateClientAsync_WithValidTaxId_StoresNormalizedValue()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        mockClientsRepo.Setup(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(BuildCodeGenerator("Cli-001").Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: "1-30-86234-6"
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Right>();
        var client = ((Either<Error, ClientDto>.Right)result).Value;
        client.TaxId.Should().Be("130862346");
    }

    [Fact]
    public async Task CreateClientAsync_WithoutTaxId_StoresNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        mockClientsRepo.Setup(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(BuildCodeGenerator("Cli-001").Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Right>();
        var client = ((Either<Error, ClientDto>.Right)result).Value;
        client.TaxId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateClientAsync_WithInvalidTaxId_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var client = new Client { Id = Guid7TestValues.ClientA, Name = "Old Name" };

        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new UpdateClientDto(
            Name: "Updated Name", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: "ABC123456"
        );

        // ACT
        var result = await service.UpdateClientAsync(Guid7TestValues.ClientA, Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto?>.Left>();
        mockClientsRepo.Verify(r => r.UpdateClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateClientAsync_WithValidTaxId_StoresNormalizedValue()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var client = new Client { Id = Guid7TestValues.ClientA, Name = "Old Name" };

        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockClientsRepo.Setup(r => r.UpdateClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new UpdateClientDto(
            Name: "Updated Name", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: "04800009575"
        );

        // ACT
        var result = await service.UpdateClientAsync(Guid7TestValues.ClientA, Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto?>.Right>();
        client.TaxId.Should().Be("04800009575");
    }

    [Fact]
    public async Task CreateClientAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();
        var mockCodes = BuildCodeGenerator("Cli-001");

        Client? capturedClient = null;
        mockClientsRepo.Setup(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Callback<Client, CancellationToken>((c, _) => capturedClient = c)
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: null
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Right>();
        var dto = ((Either<Error, ClientDto>.Right)result).Value;
        dto.Code.Should().Be("Cli-001");
        capturedClient.Should().NotBeNull();
        capturedClient!.Code.Should().Be("Cli-001");
        mockCodes.Verify(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Client,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateClientAsync_WithCode_KeepsTheGivenCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();
        var mockCodes = BuildCodeGenerator("Cli-001");

        Client? capturedClient = null;
        mockClientsRepo.Setup(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Callback<Client, CancellationToken>((c, _) => capturedClient = c)
            .Returns(Task.CompletedTask);
        mockClientsRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CUST-42", null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: "  CUST-42  "
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Right>();
        var dto = ((Either<Error, ClientDto>.Right)result).Value;
        dto.Code.Should().Be("CUST-42", "a code typed by the user wins over the generated one.");
        capturedClient!.Code.Should().Be("CUST-42");
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateClientAsync_WithDuplicatedCode_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        mockClientsRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CUST-42", null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(BuildCodeGenerator("Cli-001").Object);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new CreateClientDto(
            Name: "Client", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: "CUST-42"
        );

        // ACT
        var result = await service.CreateClientAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto>.Left>();
        var error = ((Either<Error, ClientDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockClientsRepo.Verify(r => r.AddClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateClientAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var client = new Client
        {
            Id = Guid7TestValues.ClientA, Name = "Old Name", Code = "Cli-001", CompanyId = Guid7TestValues.CompanyA
        };

        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockClientsRepo.Setup(r => r.UpdateClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new UpdateClientDto(
            Name: "Updated Name", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: null
        );

        // ACT
        var result = await service.UpdateClientAsync(Guid7TestValues.ClientA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto?>.Right>();
        client.Code.Should().Be("Cli-001", "an empty code keeps the one the client already had.");
        mockClientsRepo.Verify(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateClientAsync_WithCode_AssignsTheGivenCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var client = new Client
        {
            Id = Guid7TestValues.ClientA, Name = "Old Name", Code = "Cli-001", CompanyId = Guid7TestValues.CompanyA
        };

        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockClientsRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CUST-99", Guid7TestValues.ClientA,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockClientsRepo.Setup(r => r.UpdateClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new UpdateClientDto(
            Name: "Updated Name", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: "  CUST-99  "
        );

        // ACT
        var result = await service.UpdateClientAsync(Guid7TestValues.ClientA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto?>.Right>();
        client.Code.Should().Be("CUST-99");
        var dto = ((Either<Error, ClientDto?>.Right)result).Value;
        dto!.Code.Should().Be("CUST-99");
    }

    [Fact]
    public async Task UpdateClientAsync_WithDuplicatedCode_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var client = new Client
        {
            Id = Guid7TestValues.ClientA, Name = "Old Name", Code = "Cli-001", CompanyId = Guid7TestValues.CompanyA
        };

        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockClientsRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CUST-42", Guid7TestValues.ClientA,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);

        var service = new ClientService(mockUnitOfWork.Object);
        var request = new UpdateClientDto(
            Name: "Updated Name", Email: "client@test.com", Phone: "809-555-1234", Address: "Main St",
            ImageUrl: null, IsActive: true, CreatedAt: null, UpdatedAt: null,
            CreatedBy: null, UpdatedBy: null, TaxId: null, Code: "CUST-42"
        );

        // ACT
        var result = await service.UpdateClientAsync(Guid7TestValues.ClientA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ClientDto?>.Left>();
        var error = ((Either<Error, ClientDto?>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockClientsRepo.Verify(r => r.UpdateClientAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ICodeGenerator> BuildCodeGenerator(string nextCode)
    {
        var mockCodes = new Mock<ICodeGenerator>();
        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextCode);
        return mockCodes;
    }
}
