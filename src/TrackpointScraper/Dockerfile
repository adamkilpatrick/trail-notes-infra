FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.fsproj .
RUN dotnet restore
COPY . .


RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/playwright/dotnet:v1.57.0-noble

# Install dependencies including curl and Playwright requirements
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libatk-bridge2.0-0 \
    libdrm2 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    xdg-utils \
    libxss1 \
    && rm -rf /var/lib/apt/lists/*

# Install Lambda Runtime Interface Client
RUN curl -Lo /usr/local/bin/aws-lambda-rie https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie \
    && chmod +x /usr/local/bin/aws-lambda-rie

WORKDIR /var/task
COPY --from=build /app/publish .

ENTRYPOINT ["/usr/local/bin/aws-lambda-rie"]
CMD ["dotnet", "TrackpointScraper.dll", "TrackpointScraper.Function::FunctionHandler"]