FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
ENV ASPNETCORE_URLS="" ASPNETCORE_HTTP_PORTS=""
EXPOSE 8080 8998

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Scaler/Scaler.csproj", "src/Scaler/"]
RUN dotnet restore "./src/Scaler/Scaler.csproj"
COPY . .
WORKDIR "/src/src/Scaler"
RUN dotnet build "./Scaler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Scaler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Scaler.dll"]
