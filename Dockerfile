# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj files and restore dependencies
COPY PlanningPulse.sln ./
COPY src/PlanningPulse.Domain/PlanningPulse.Domain.csproj src/PlanningPulse.Domain/
COPY src/PlanningPulse.Application/PlanningPulse.Application.csproj src/PlanningPulse.Application/
COPY src/PlanningPulse.Infrastructure/PlanningPulse.Infrastructure.csproj src/PlanningPulse.Infrastructure/
COPY src/PlanningPulse.Web/PlanningPulse.Web.csproj src/PlanningPulse.Web/
COPY tests/PlanningPulse.Tests/PlanningPulse.Tests.csproj tests/PlanningPulse.Tests/

RUN dotnet restore

# Copy the remaining source code
COPY . .

# Build and publish the web app
WORKDIR /app/src/PlanningPulse.Web
RUN dotnet publish -c Release -o /out

# Use the official ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Expose port 5000 and configure the app to listen on it
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# Set entry point
ENTRYPOINT ["dotnet", "PlanningPulse.Web.dll"]
