using Microsoft.AspNetCore.Mvc;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/protocols")]
public class ProtocolsController : ControllerBase
{
    private readonly IProtocolFactory _protocolFactory;

    public ProtocolsController(IProtocolFactory protocolFactory)
    {
        _protocolFactory = protocolFactory;
    }

    [HttpGet]
    public IActionResult GetAll()
        => Ok(_protocolFactory.GetProtocolMetadata());
}
