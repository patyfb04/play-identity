FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src

# Copy nuget.config and packages folder
COPY nuget.config ./
COPY packages ./packages

COPY ["Play.Identity/src/Play.Identity.Service/Play.Identity.Service.csproj", "Play.Identity/src/Play.Identity.Service/"]
COPY ["Play.Identity/src/Play.Identity.Contracts/Play.Identity.Contracts.csproj", "Play.Identity/src/Play.Identity.Contracts/"]
RUN dotnet restore "Play.Identity/src/Play.Identity.Service/Play.Identity.Service.csproj"
COPY . .
WORKDIR "/src/Play.Identity/src/Play.Identity.Service"
RUN dotnet build "Play.Identity.Service.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Play.Identity.Service.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Play.Identity.Service.dll"]
