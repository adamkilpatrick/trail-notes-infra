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

# Install Node.js
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs

WORKDIR /var/task
COPY --from=build /app/publish .
RUN mkdir -p /var/task/ms-playwright/
ENV PLAYWRIGHT_BROWSERS_PATH=/var/task/ms-playwright

# Install Playwright browsers
#RUN npx playwright@1.57.0 install chromium --with-deps

# Debug: Check where browsers are installed
#RUN find / -name "chrome" -type f 2>/dev/null | head -10 || true
#RUN ls -la /root/.cache/ || true

# Create symlink from Node.js Playwright location to .NET Playwright expected location
#RUN mkdir -p /root/.cache/ms-playwright/chromium-1140/chrome-linux/ && \
#    CHROME_PATH=$(find /root/.cache -name "chrome" -type f | head -1) && \
#    if [ -n "$CHROME_PATH" ]; then ln -sf "$CHROME_PATH" /root/.cache/ms-playwright/chromium-1140/chrome-linux/chrome; fi

# Set Playwright browser path for .NET


RUN pwsh playwright.ps1 install

# Fix Playwright permissions
RUN find /var/task -type f -name "node" -exec chmod +x {} \; || true
RUN find /var/task -type f -name "*.sh" -exec chmod +x {} \; || true
RUN find /root/.cache/ms-playwright -type f -name "chrome" -exec chmod +x {} \; || true

ENTRYPOINT ["./TrackpointScraper"]