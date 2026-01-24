# TrackpointScraper

F# project for web scraping with headless browser automation and AJAX interception.

## Setup

1. Install Playwright browsers:
   ```
   dotnet run --project . -- install
   ```
   Or manually:
   ```
   pwsh bin/Debug/net9.0/playwright.ps1 install
   ```

2. Build the project:
   ```
   dotnet build
   ```

3. Run the scraper:
   ```
   dotnet run
   ```

## Container Usage

### Build the container:
```bash
docker build -t trackpoint-scraper .
```

### Run locally for testing:
```bash
docker run -p 9000:8080 trackpoint-scraper
```

### Invoke the Lambda function:
```bash
curl -XPOST "http://localhost:9000/2015-03-31/functions/function/invocations" -d '{}'
```

### Deploy to AWS Lambda:
1. Push image to ECR
2. Create Lambda function using container image
3. Set handler to: `TrackpointScraper.Function::FunctionHandler`

## Usage

Modify `Program.fs` to:
- Set your target URL in `targetUrl`
- Set the AJAX URL pattern to intercept in `ajaxPattern`
- Add any additional page interactions before the AJAX call

The scraper will:
1. Launch a headless Chrome browser
2. Navigate to the target page
3. Intercept AJAX calls matching your pattern
4. Return the captured response data

## Customization

- Adjust the timeout in the main loop (currently 30 seconds)
- Add page interactions like clicks, form fills, etc. before waiting for AJAX
- Modify the `AjaxResponse` type to capture additional response data
- Add error handling and retry logic as needed