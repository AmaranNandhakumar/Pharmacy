# Pharmacy API: build with the .NET SDK, run on the smaller ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, from the project files only, so this layer is cached until a dependency changes
COPY Pharmacy.sln ./
COPY Pharmacy.Core/Pharmacy.Core.csproj Pharmacy.Core/
COPY Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj Pharmacy.Infrastructure/
COPY Pharmacy.Api/Pharmacy.Api.csproj Pharmacy.Api/
RUN dotnet restore Pharmacy.Api/Pharmacy.Api.csproj

COPY Pharmacy.Core/ Pharmacy.Core/
COPY Pharmacy.Infrastructure/ Pharmacy.Infrastructure/
COPY Pharmacy.Api/ Pharmacy.Api/
RUN dotnet publish Pharmacy.Api/Pharmacy.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Run as the image's built-in non-root user
USER $APP_UID
ENTRYPOINT ["dotnet", "Pharmacy.Api.dll"]
