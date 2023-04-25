namespace CustomDatabase

#nowarn "20"

open System.Text.Json.Serialization
open CustomDatabase.Value
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.OpenApi.Models


module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)

        let info = OpenApiInfo()
        info.Title <- "My API V1"
        info.Version <- "v1"

        builder.Services
            .AddControllers()
            .AddJsonOptions(fun options ->
                JsonFSharpOptions
                    .FSharpLuLike()
                    .AddToJsonSerializerOptions(options.JsonSerializerOptions)

                options.JsonSerializerOptions.Converters.Insert(0, ValueResolver()))

        builder.Services.AddSwaggerGen(fun config -> config.SwaggerDoc("v1", info))


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

        exitCode
