FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["OnlineMusicLibrary/OnlineMusicLibrary.csproj", "OnlineMusicLibrary/"]
RUN dotnet restore "OnlineMusicLibrary/OnlineMusicLibrary.csproj"
COPY . .
WORKDIR "/src/OnlineMusicLibrary"
RUN dotnet build "OnlineMusicLibrary.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OnlineMusicLibrary.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OnlineMusicLibrary.dll"]
