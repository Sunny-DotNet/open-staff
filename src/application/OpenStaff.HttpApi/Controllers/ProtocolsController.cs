
using Microsoft.AspNetCore.Mvc;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 协议元数据控制器。
/// Controller that exposes provider protocol metadata.
/// </summary>
[ApiController]
[Route("api/protocols")]
public class ProtocolsController : ControllerBase
{
    private readonly IProtocolFactory _protocolFactory;

    /// <summary>
    /// 初始化协议元数据控制器。
    /// Initializes the protocol metadata controller.
    /// </summary>
    /// <param name="protocolFactory">注入的协议工厂，用于解析已注册协议的元数据，控制器通过它将底层协议注册信息映射为 HTTP 响应。 / Injected protocol factory used to resolve metadata for registered protocols so the controller can map underlying protocol registrations to HTTP responses.</param>
    public ProtocolsController(IProtocolFactory protocolFactory)
    {
        _protocolFactory = protocolFactory;
    }

    /// <summary>
    /// 获取所有协议元数据。
    /// Gets metadata for all registered protocols.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<ProtocolMetadata>> GetAll()
        => Ok(_protocolFactory.GetProtocolMetadata());
}
