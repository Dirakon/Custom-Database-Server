namespace CustomDatabase

#nowarn "20"

open System.Text.Json.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.OpenApi.Models
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open QueryLanguage


module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)

        let info = OpenApiInfo()
        info.Title <- "My API V1"
        info.Version <- "v1"
        builder.Services.AddControllers()
        builder.Services.AddSwaggerGen(fun config -> config.SwaggerDoc("v1", info))

        builder.Services
            .AddControllersWithViews() // or whichever method you're using to get an IMvcBuilder
            .AddNewtonsoftJson()

        let app = builder.Build()

        app.UseHttpsRedirection()

        app.UseAuthorization()
        app.MapControllers()

        if app.Environment.IsDevelopment() then
            do
                app.UseDeveloperExceptionPage()
                app.UseSwagger()
                app.UseSwaggerUI(fun config -> config.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"))



        app.Run()
        Class1.DoStuff();
        exitCode
