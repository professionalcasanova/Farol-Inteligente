using Microsoft.AspNetCore.Mvc;

namespace Farol.Tests.Api;

[ApiController]
[Route("api/test/errors")]
public sealed class TestErrorsController : ControllerBase
{
    [HttpGet("throw")]
    public ActionResult Throw()
    {
        throw new InvalidOperationException("Sensitive internal exception detail");
    }
}
