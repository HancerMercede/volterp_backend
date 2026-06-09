using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_WithDuplicateUsername_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();
        var mockPasswordHasher = new Mock<Volterp.Application.Interfaces.IPasswordHasher>();

        mockUsersRepo.Setup(r => r.GetByUsernameAsync("existinguser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "existinguser" });
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, mockPasswordHasher.Object);
        var request = new CreateUserDto("existinguser", "password123", "test@example.com", "Test User", Domain.Enums.UserRole.Ventas, 1);

        // ACT
        var result = await service.CreateAsync(request);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Left>();
        var error = ((Either<Error, UserDto>.Left)result).Value;
        error.Message.Should().Be("username already exists");
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_PasswordIsHashed()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();
        var mockPasswordHasher = new Mock<Volterp.Application.Interfaces.IPasswordHasher>();

        mockUsersRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUsersRepo.Setup(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken ct) => u);
        mockPasswordHasher.Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/X4.G4j-hpFGqAZa8O");
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new UserService(mockUnitOfWork.Object, mockPasswordHasher.Object);
        var request = new CreateUserDto("newuser", "plainpassword", "test@example.com", "Test User", Domain.Enums.UserRole.Ventas, 1);

        // ACT
        var result = await service.CreateAsync(request);

        // ASSERT - verify password hasher was called with the plain password
        mockPasswordHasher.Verify(
            h => h.Hash("plainpassword"),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var users = new PagedResult<User>
        {
            Items = new List<User>
            {
                new() { Id = 1, Username = "user1", Email = "user1@test.com", FullName = "User One", Role = Domain.Enums.UserRole.Ventas, CompanyId = 1, IsActive = true },
                new() { Id = 2, Username = "user2", Email = "user2@test.com", FullName = "User Two", Role = Domain.Enums.UserRole.Inventario, CompanyId = 1, IsActive = true }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockUsersRepo.Setup(r => r.GetAllByCompanyAsync(1, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetAllAsync(1, 1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsUserDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var user = new User
        {
            Id = 1, Username = "testuser", Email = "test@test.com",
            FullName = "Test User", Role = Domain.Enums.UserRole.Ventas,
            CompanyId = 1, IsActive = true
        };

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetByIdAsync(1);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Right>();
        var dto = ((Either<Error, UserDto>.Right)result).Value;
        dto.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetByIdAsync(999);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Left>();
        var error = ((Either<Error, UserDto>.Left)result).Value;
        error.Message.Should().Be("user not found");
    }

    [Fact]
    public async Task UpdateAsync_WhenUserNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());
        var request = new UserWithPasswordHashDto
        {
            Id = 999, Username = "updated", Email = "updated@test.com",
            FullName = "Updated User", Role = Domain.Enums.UserRole.Ventas,
            IsActive = true, CompanyId = 1, PasswordHash = "hash"
        };

        // ACT
        var result = await service.UpdateAsync(999, request);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Left>();
        var error = ((Either<Error, UserDto>.Left)result).Value;
        error.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateAsync_WithValidInput_UpdatesUser()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var user = new User
        {
            Id = 1, Username = "testuser", Email = "old@test.com",
            FullName = "Old Name", Role = Domain.Enums.UserRole.Ventas,
            CompanyId = 1, IsActive = true
        };

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUsersRepo.Setup(r => r.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());
        var request = new UserWithPasswordHashDto
        {
            Id = 1, Username = "testuser", Email = "new@test.com",
            FullName = "New Name", Role = Domain.Enums.UserRole.Admin,
            IsActive = true, CompanyId = 1, PasswordHash = user.PasswordHash
        };

        // ACT
        var result = await service.UpdateAsync(1, request);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Right>();
        user.Email.Should().Be("new@test.com");
        user.FullName.Should().Be("New Name");
        mockUsersRepo.Verify(r => r.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.DeleteAsync(999);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteAsync_WhenUserFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var user = new User
        {
            Id = 1, Username = "testuser", Email = "test@test.com",
            FullName = "Test User", Role = Domain.Enums.UserRole.Ventas,
            CompanyId = 1, IsActive = true
        };

        mockUsersRepo.Setup(r => r.GetUserByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUsersRepo.Setup(r => r.DeleteUserAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.DeleteAsync(1);

        // ASSERT
        result.Should().BeOfType<Either<Error, int>.Right>();
        mockUsersRepo.Verify(r => r.DeleteUserAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenFound_ReturnsUserDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var user = new User
        {
            Id = 1, Username = "testuser", Email = "test@test.com",
            FullName = "Test User", Role = Domain.Enums.UserRole.Ventas,
            CompanyId = 1, IsActive = true
        };

        mockUsersRepo.Setup(r => r.GetByUserByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetByEmailAsync("test@test.com");

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Right>();
        var dto = ((Either<Error, UserDto>.Right)result).Value;
        dto.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        mockUsersRepo.Setup(r => r.GetByUserByEmailAsync("notfound@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetByEmailAsync("notfound@test.com");

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Left>();
        var error = ((Either<Error, UserDto>.Left)result).Value;
        error.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenFound_ReturnsUserWithPasswordHashDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        var user = new User
        {
            Id = 1, Username = "testuser", Email = "test@test.com",
            FullName = "Test User", Role = Domain.Enums.UserRole.Ventas,
            CompanyId = 1, IsActive = true, PasswordHash = "somehash"
        };

        mockUsersRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<Volterp.Application.Interfaces.IPasswordHasher>());

        // ACT
        var result = await service.GetByUsernameAsync("testuser");

        // ASSERT
        result.Should().BeOfType<Either<Error, UserWithPasswordHashDto>.Right>();
        var dto = ((Either<Error, UserWithPasswordHashDto>.Right)result).Value;
        dto.Username.Should().Be("testuser");
        dto.PasswordHash.Should().Be("somehash");
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();

        mockUsersRepo.Setup(r => r.GetByUsernameAsync("notfound", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, Mock.Of<IPasswordHasher>());

        // ACT
        var result = await service.GetByUsernameAsync("notfound");

        // ASSERT
        result.Should().BeOfType<Either<Error, UserWithPasswordHashDto>.Left>();
        var error = ((Either<Error, UserWithPasswordHashDto>.Left)result).Value;
        error.Message.Should().Be("User not found");
    }
}
