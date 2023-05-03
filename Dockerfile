FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /App

# Install java runtime to generate code with ANTLR
RUN apt-get update && \
apt-get install -y --no-install-recommends \
        openjdk-11-jre 
    

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /App

# Install curl for health-checks
RUN apt-get update && \
     apt-get install -y curl jq 

COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "CustomDatabase.dll"]
