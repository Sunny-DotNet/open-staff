using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Entities;

public abstract class EntityBase<TKey>
{
    /// <summary>账户唯一标识 / Unique provider account identifier.</summary>
    public TKey Id { get; set; } =default!;
}
public abstract class EntityBase : EntityBase<Guid>
{
    public EntityBase()
    {
        Id = Guid.NewGuid();
    }
}

public interface IMustHaveCreatedAt
{
    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    DateTime CreatedAt { get; set; }
}
public interface IMayHaveUpdatedAt
{
    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    DateTime? UpdatedAt { get; set; }
}