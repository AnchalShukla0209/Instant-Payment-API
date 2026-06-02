using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : ControllerBase
{
    [Route("/error")]
    public IActionResult HandleError()
    {
        return Problem(
            title: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError,
            detail: null
        );
    }

    [Route("/error/{statusCode:int}")]
    public IActionResult HandleStatusCode(int statusCode)
    {
        var title = statusCode switch
        {
            400 => "The request was invalid.",
            401 => "Authentication is required.",
            403 => "You do not have permission to access this resource.",
            404 => "The requested resource was not found.",
            405 => "The HTTP method is not allowed.",
            429 => "Too many requests. Please try again later.",
            _   => "An error occurred processing your request."
        };

        return Problem(title: title, statusCode: statusCode, detail: null);
    }
}
