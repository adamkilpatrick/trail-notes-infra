namespace ImageLocationExtractor

open System
open System.IO
open System.Text.Json
open Amazon.Lambda.Core
open Amazon.S3
open Amazon.S3.Model
open MetadataExtractor
open MetadataExtractor.Formats.Exif

[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
do ()

type LocationData = {
    Latitude: float
    Longitude: float
}

type ImageLocations = Map<string, LocationData>

type SQSEvent = {
    Records: SQSRecord[]
}
and SQSRecord = {
    Body: string
}

type S3Event = {
    Records: S3EventRecord[]
}
and S3EventRecord = {
    S3: S3Data
}
and S3Data = {
    Bucket: S3Bucket
    Object: S3Object
}
and S3Bucket = {
    Name: string
}
and S3Object = {
    Key: string
}

module ExifExtractor =
    let extractGpsCoordinates (stream: Stream) =
        let directories = ImageMetadataReader.ReadMetadata(stream)
        let gpsDirectory = directories |> Seq.tryFind (fun d -> d :? GpsDirectory) |> Option.map (fun d -> d :?> GpsDirectory)
        
        match gpsDirectory with
        | Some gps when gps.HasTagName(GpsDirectory.TagLatitude) && gps.HasTagName(GpsDirectory.TagLongitude) ->
            let lat = gps.GetGeoLocation().Latitude
            let lng = gps.GetGeoLocation().Longitude
            Some { Latitude = lat; Longitude = lng }
        | _ -> None
    
    let extractFromFile (filePath: string) =
        use stream = File.OpenRead(filePath)
        extractGpsCoordinates stream

module S3Helper =
    let private s3Client = new AmazonS3Client()
    
    let downloadImage bucketName key =
        async {
            let request = GetObjectRequest(BucketName = bucketName, Key = key)
            let! response = s3Client.GetObjectAsync(request) |> Async.AwaitTask
            let memoryStream = new MemoryStream()
            do! response.ResponseStream.CopyToAsync(memoryStream) |> Async.AwaitTask
            memoryStream.Position <- 0L
            return memoryStream
        }
    
    let downloadJson bucketName key =
        async {
            try
                let request = GetObjectRequest(BucketName = bucketName, Key = key)
                let! response = s3Client.GetObjectAsync(request) |> Async.AwaitTask
                use reader = new StreamReader(response.ResponseStream)
                let! content = reader.ReadToEndAsync() |> Async.AwaitTask
                return JsonSerializer.Deserialize<Map<string, LocationData>>(content)
            with
            | _ -> return Map.empty
        }
    
    let uploadJson bucketName key (data: ImageLocations) =
        async {
            let json = JsonSerializer.Serialize(data)
            use stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))
            let request = PutObjectRequest(BucketName = bucketName, Key = key, InputStream = stream, ContentType = "application/json")
            let! _ = s3Client.PutObjectAsync(request) |> Async.AwaitTask
            return ()
        }

type Function() =
    member _.FunctionHandler (sqsEvent: SQSEvent) (context: ILambdaContext) =
        async {
            let jsonBucket = Environment.GetEnvironmentVariable("JSON_BUCKET")
            let jsonKey = Environment.GetEnvironmentVariable("JSON_KEY")
            context.Logger.LogInformation($"Event {sqsEvent}")

            for record in sqsEvent.Records do
                let options = new JsonSerializerOptions(PropertyNameCaseInsensitive = true)
                let s3Event = JsonSerializer.Deserialize<S3Event>(record.Body, options)
                context.Logger.LogInformation($"SQSRECORD {s3Event}")
                
                for s3Record in s3Event.Records do
                    context.Logger.LogInformation(JsonSerializer.Serialize(s3Record))
                    context.Logger.LogInformation($"S3RECORD {s3Record}")
                    let bucketName = s3Record.S3.Bucket.Name
                    let objectKey = s3Record.S3.Object.Key
                    
                    context.Logger.LogInformation($"Processing {objectKey} from {bucketName}")
                    
                    try
                        use! imageStream = S3Helper.downloadImage bucketName objectKey
                        
                        match ExifExtractor.extractGpsCoordinates imageStream with
                        | Some location ->
                            let! currentData = S3Helper.downloadJson jsonBucket jsonKey
                            let updatedData = currentData |> Map.add objectKey location
                            do! S3Helper.uploadJson jsonBucket jsonKey updatedData
                            context.Logger.LogInformation($"Updated location for {objectKey}: {location.Latitude}, {location.Longitude}")
                        | None ->
                            context.Logger.LogInformation($"No GPS data found in {objectKey}")
                    with
                    | ex -> context.Logger.LogError($"Error processing {objectKey}: {ex.Message}")
        } |> Async.RunSynchronously

module Program =
    [<EntryPoint>]
    let main args =
        match args with
        | [| filePath |] when File.Exists(filePath) ->
            match ExifExtractor.extractFromFile filePath with
            | Some location ->
                printfn $"GPS found in {Path.GetFileName(filePath)}: {location.Latitude}, {location.Longitude}"
            | None ->
                printfn $"No GPS data found in {Path.GetFileName(filePath)}"
            0
        | _ ->
            printfn "Usage: dotnet run <image-file-path>"
            printfn "Example: dotnet run photo.jpg"
            1