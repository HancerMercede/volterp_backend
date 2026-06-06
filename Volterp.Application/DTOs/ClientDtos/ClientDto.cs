using Volterp.Domain.Entities;
namespace Volterp.Application.DTOs.ClientDtos;

public record ClientDto:IMapFrom<Client>
{
    public int Id { get; set; }
    public  string Name { get; set; } = string.Empty;
    public  string Email { get; set; } = string.Empty;
    public  string Phone { get; set; } = string.Empty;
    public  string Address { get; set; } = string.Empty;
    public string? ImageUrl  { get; set; }
    public bool IsActive  { get; set; }
    public DateTime? CreatedAt  { get; set; }
    public DateTime? UpdatedAt  { get; set; }
    public Guid? CreatedBy   { get; set; }
    public Guid? UpdatedBy    { get; set; }
    
    public void MapFrom(Client source)
    {
        Id = source.Id;
        Name = source.Name;
        Email = source.Email;
        Phone = source.Phone;
        Address = source.Address;
        ImageUrl = source.ImageUrl;
        IsActive = source.IsActive;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
    }
} 

