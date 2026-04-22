using OpenStaff.Dtos;
using OpenStaff.Entities;
using Riok.Mapperly.Abstractions;

namespace OpenStaff.Application.Providers.Services;
[Mapper(AllowNullPropertyAssignment = false)]
public partial class ProviderAccountMapper
{
    public partial ProviderAccountDto ToDto(ProviderAccount source);

    [MapperIgnoreTarget(nameof(ProviderAccount.Id))]
    [MapperIgnoreTarget(nameof(ProviderAccount.CreatedAt))]
    [MapperIgnoreTarget(nameof(ProviderAccount.UpdatedAt))]
    public partial ProviderAccount ToEntity(CreateProviderAccountInput source);

    [MapperIgnoreTarget(nameof(ProviderAccount.Id))]
    [MapperIgnoreTarget(nameof(ProviderAccount.ProtocolType))]
    [MapperIgnoreTarget(nameof(ProviderAccount.CreatedAt))]
    [MapperIgnoreTarget(nameof(ProviderAccount.UpdatedAt))]
    public partial void Apply(UpdateProviderAccountInput source, [MappingTarget] ProviderAccount target);
}

