using Microsoft.AspNetCore.Mvc;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 协议元数据控制器 — 返回所有已注册协议及其 EnvSchema
/// </summary>
[ApiController]
[Route("api/protocols")]
public class ProtocolsController : ControllerBase
{
    private readonly IProtocolFactory _protocolFactory;

    public ProtocolsController(IProtocolFactory protocolFactory)
    {
        _protocolFactory = protocolFactory;
    }

    /// <summary>
    /// 获取所有已注册协议的元数据（含 EnvSchema）
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        var metadata = _protocolFactory.GetProtocolMetadata();
        return Ok(metadata);
    }
}
