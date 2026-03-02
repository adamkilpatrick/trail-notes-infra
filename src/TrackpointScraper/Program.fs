namespace TrackpointScraper

open System
open Amazon.S3
open Amazon.S3.Model
open System.Threading.Tasks
open Microsoft.Playwright
open System.Text.Json
open System.IO
open Amazon.Lambda.Core
open Amazon.Lambda.RuntimeSupport
open Amazon.Lambda.Serialization.SystemTextJson

[<assembly: LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
do ()
module TrackpointScraper = 
    type AjaxResponse = {
        Url: string
        Method: string
        Status: int
        ResponseBody: string
    }
    type TrackPointPosition = {
        Lat: float
        Lon: float
    }
    type TrackPoint = {
        DateTime: string
        Position: TrackPointPosition
        Altitude: float
    }
    type ResponseBody = {
        TrackPoints: TrackPoint[]
    }

    type LocationPoint = {
        Loc: float[]
        Label: string
    }

    type LocationPath = {
        Root: string
        Path: LocationPoint[]
    }

    let runScraper (targetUrl: string) (ajaxPattern: string) = task {
        use! playwright = Playwright.CreateAsync()
        let options = BrowserTypeLaunchOptions()
        options.Headless <- true
        options.Timeout <- 90000f
        options.ChromiumSandbox <- false
        options.Args <- [|"--disable-gpu"; "--single-process"|]
        let! browser = playwright.Chromium.LaunchAsync(options)
        let! context = browser.NewContextAsync()
        let! page = context.NewPageAsync()
        
        // Set up response interception
        let capturedResponses = ResizeArray<AjaxResponse>()
        
        page.Response.Add(fun response ->
            if response.Url.Contains(ajaxPattern) then
                task {
                    let! responseBody = response.TextAsync()
                    let ajaxResponse = {
                        Url = response.Url
                        Method = response.Request.Method
                        Status = response.Status
                        ResponseBody = responseBody
                    }
                    capturedResponses.Add(ajaxResponse)
                    printfn "Captured AJAX response from: %s" response.Url
                } |> ignore
        )
        
        // Navigate to target page
        let! _ = page.GotoAsync(targetUrl)
        do! Task.Delay(1500)
        let! _ = page.GotoAsync(targetUrl)
        do! Task.Delay(1500) // garmin does some weird redirect sometimes, this is a hack
        
        // Wait for AJAX calls to complete (adjust timeout as needed)
        let mutable attempts = 0
        while attempts < 30 do
            do! Task.Delay(500)
            // Scroll to bottom of page
            let! _ = page.EvaluateAsync($"window.scrollTo(0, document.body.scrollHeight/30*{attempts})")
            attempts <- attempts + 1
        
        do! browser.CloseAsync()
        
        return List.ofSeq capturedResponses
    }

    let downloadPath (s3Client: AmazonS3Client) (bucketName: string) (pathName: string) =
        async {
            let options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            let request = GetObjectRequest(BucketName = bucketName, Key = $"paths/{pathName}.json")
            let! response = s3Client.GetObjectAsync(request) |> Async.AwaitTask
            use reader = new StreamReader(response.ResponseStream)
            let! content = reader.ReadToEndAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<LocationPath>(content, options).Path
        }

    let uploadPath (s3Client: AmazonS3Client) bucketName (pathName: string) (data: LocationPath) =
        async {
            let options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            let json = JsonSerializer.Serialize(data, options)
            use stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))
            let request = PutObjectRequest(BucketName = bucketName, Key = $"paths/{pathName}.json", InputStream = stream, ContentType = "application/json")
            let! response = s3Client.PutObjectAsync(request) |> Async.AwaitTask
            printfn "Upload response: %A" response.HttpStatusCode
            return ()
        }

    let convertResponseToPath (ajaxResponse:AjaxResponse) : LocationPoint[] =
        let options = new JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        let responseBody = JsonSerializer.Deserialize<ResponseBody>(ajaxResponse.ResponseBody, options)
        responseBody.TrackPoints
        |> Array.map (fun point -> {Label=point.DateTime; Loc=[|point.Position.Lat; point.Position.Lon; point.Altitude|]})

    let getGarminPath (targetUrl: string) = task {
        let! scrapedData = runScraper targetUrl "api/sessions/"

        let returnPath = scrapedData
                        |> List.map convertResponseToPath 
                        |> Array.concat
        return returnPath
    }

    let mergePaths (newPathData: LocationPoint[]) (existingPathData: LocationPoint[]) =
        [|newPathData; existingPathData|]
        |> Array.concat
        |> Array.groupBy (fun n -> n.Label)
        |> Array.map (fun (_, n) -> Array.head n)
        |> Array.sortBy (fun n -> n.Label)

    let functionHandler (input: obj) (context: ILambdaContext) : Task<string> = task {
        let s3Client = new AmazonS3Client()
        let targetUrl = Environment.GetEnvironmentVariable("TARGET_URL")
        let bucketName = Environment.GetEnvironmentVariable("S3_BUCKET")
        let pathName = Environment.GetEnvironmentVariable("PATH_NAME")
        let root = Environment.GetEnvironmentVariable("ROOT")

        try
            let! updatedGarminPoints = getGarminPath targetUrl
            context.Logger.LogInformation("Scraped points from Garmin")
            let! existingGarminPath = downloadPath s3Client bucketName pathName |> Async.StartAsTask
            context.Logger.LogInformation("Loaded existing Garmin path")

            let combinedPaths = mergePaths updatedGarminPoints existingGarminPath
            let outputPath = {Root=root; Path=combinedPaths}
            context.Logger.LogInformation("Merged existing Garmin path")
            do! uploadPath s3Client bucketName pathName outputPath |> Async.StartAsTask
            
            context.Logger.LogInformation($"Successfully processed {combinedPaths.Length} points")
            return "Success"
        with
        | ex -> 
            context.Logger.LogError($"Error: {ex.Message}")
            raise ex
            return "Error"
    }

module Program =
    [<EntryPoint>]
    let main _args =
        let handler = Func<obj, ILambdaContext, Task<string>>(TrackpointScraper.functionHandler)
        use bootstrap = LambdaBootstrapBuilder.Create<obj, string>(handler, new DefaultLambdaJsonSerializer()).Build()
        bootstrap.RunAsync().GetAwaiter().GetResult()
        0
