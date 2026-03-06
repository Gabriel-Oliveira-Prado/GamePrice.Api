FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["GamePrice.Api/GamePrice.Api.csproj", "GamePrice.Api/"]
RUN dotnet restore "GamePrice.Api/GamePrice.Api.csproj"
COPY . .
WORKDIR "/src/GamePrice.Api"
RUN dotnet publish "GamePrice.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Configura a porta padrão para 5200 dentro do container
ENV ASPNETCORE_URLS=http://+:5200
EXPOSE 5200

ENTRYPOINT ["dotnet", "GamePrice.Api.dll"]