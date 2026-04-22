using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Dtos;

internal class DtoBase
{
}

/// <summary>
/// 通用分页结果。
/// Generic paged result envelope.
/// </summary>
/// <typeparam name="T">分页项类型。 / Item type contained in the page.</typeparam>

public record struct PagedResult<T>(List<T> Items,int Total);

public interface IPagingInput { 
    int Page { get; set; }
    int PageSize { get; set; }
}