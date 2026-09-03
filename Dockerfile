FROM node:24-alpine AS web-build
WORKDIR /web
COPY src/RoomBooking.Web/package.json src/RoomBooking.Web/package-lock.json ./
RUN npm ci
COPY src/RoomBooking.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /source
COPY global.json ./
COPY src/RoomBooking.Domain/RoomBooking.Domain.csproj src/RoomBooking.Domain/
COPY src/RoomBooking.Application/RoomBooking.Application.csproj src/RoomBooking.Application/
COPY src/RoomBooking.Infrastructure/RoomBooking.Infrastructure.csproj src/RoomBooking.Infrastructure/
COPY src/RoomBooking.Api/RoomBooking.Api.csproj src/RoomBooking.Api/
RUN dotnet restore src/RoomBooking.Api/RoomBooking.Api.csproj
COPY src/ ./src/
RUN dotnet publish src/RoomBooking.Api/RoomBooking.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api-build /app/publish ./
COPY --from=web-build /web/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet RoomBooking.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
