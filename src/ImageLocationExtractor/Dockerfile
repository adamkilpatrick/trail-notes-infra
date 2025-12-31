FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.fsproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM public.ecr.aws/lambda/dotnet:9
COPY --from=build /app/publish/ ${LAMBDA_TASK_ROOT}
CMD ["ImageLocationExtractor::ImageLocationExtractor.Function::FunctionHandler"]