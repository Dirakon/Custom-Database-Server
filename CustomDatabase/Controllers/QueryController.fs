namespace CustomDatabase.Controllers


open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>) =
    inherit ControllerBase()

    let summaries =
        [| "Freezing"
           "Bracing"
           "Chilly"
           "Cool"
           "Mild"
           "Warm"
           "Balmy"
           "Hot"
           "Sweltering"
           "Scorching" |]

    [<HttpGet>]
    member _.Get(query: string) = ""

    [<HttpPost>]
    member _.Add(query: string) = ""

    [<HttpPut>]
    member _.Update(query: string) = ""

    [<HttpDelete>]
    member _.Delete(query: string) = ""
