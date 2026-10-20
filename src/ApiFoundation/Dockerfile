# Etapa 1: compila a API usando a imagem com o SDK do .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia só o .csproj primeiro para o restore ficar em cache entre builds
COPY src/ApiFoundation/ApiFoundation.csproj ApiFoundation/
RUN dotnet restore ApiFoundation/ApiFoundation.csproj

COPY src/ApiFoundation/ ApiFoundation/
RUN dotnet publish ApiFoundation/ApiFoundation.csproj -c Release -o /app --no-restore

# Etapa 2: imagem final, só com o runtime do ASP.NET (bem menor que a do SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiFoundation.dll"]