FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached when only the code changes.
COPY EvalLab.Api/EvalLab.Api.csproj EvalLab.Api/
RUN dotnet restore EvalLab.Api/EvalLab.Api.csproj

COPY EvalLab.Api/ EvalLab.Api/
RUN dotnet publish EvalLab.Api/EvalLab.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

# Non-root user provided by the .NET base images.
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "EvalLab.Api.dll"]
