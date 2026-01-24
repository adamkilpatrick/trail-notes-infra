import json
import boto3
import os

s3 = boto3.client('s3')
cf_client = boto3.client('cloudfront')

def lambda_handler(event, context):
    paths = os.environ['PATHS'].split(',')
    output_key = os.environ['OUTPUT_KEY']
    cf_distribution_id = os.environ['CLOUDFRONT_DISTRIBUTION_ID']
    bucket = event['Records'][0]['s3']['bucket']['name']
    
    merged_path = []
    root = ''
    
    for path in paths:
        obj = s3.get_object(Bucket=bucket, Key='paths/'+path+'.json')
        data = json.loads(obj['Body'].read())
        root = data['root']
        merged_path.extend(data['path'])
    
    merged_path.sort(key=lambda x: x['label'])
    
    s3.put_object(
        Bucket=bucket,
        Key=output_key,
        Body=json.dumps({'root': root, 'path': merged_path}),
        ContentType='application/json'
    )
    cf_client.create_invalidation(
        DistributionId=cf_distribution_id,
        InvalidationBatch={
            'Paths': {
                'Quantity': 1,
                'Items': ['/status/*']
            },
            'CallerReference': str(time.time()) 
        }
    )
    
    return {'statusCode': 200}