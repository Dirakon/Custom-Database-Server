namespace CustomDatabase

#nowarn "20"

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.OpenApi.Models


module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)

        builder.Services
            .AddControllers()
            .AddJsonOptions(fun options -> JsonConverter.addConvertersTo options.JsonSerializerOptions)

        let info = OpenApiInfo()
        info.Title <- "My API V1"
        info.Version <- "v1"
        builder.Services.AddSwaggerGen(fun config -> config.SwaggerDoc("v1", info))

        builder.Services.AddSingleton<IDataStorage, DataStorage>()

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
