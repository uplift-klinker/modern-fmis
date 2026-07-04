FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY backend/src/Fmis.Api/Fmis.Api.csproj backend/src/Fmis.Api/
COPY backend/src/Fmis.Core/Fmis.Core.csproj backend/src/Fmis.Core/
COPY backend/src/Fmis.Models/Fmis.Models.csproj backend/src/Fmis.Models/
RUN dotnet restore backend/src/Fmis.Api/Fmis.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/src/Fmis.Api/Fmis.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fmis.Api.dll"]
