namespace CustomDatabase.Controllers


open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

[<ApiController>]
[<Route("[controller]")>]
type HealthCheckController(logger: ILogger<HealthCheckController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.HealthCheck() = "Ok"
