import urllib.request, json, os, boto3, time
from zoneinfo import ZoneInfo
from datetime import datetime, timezone
from datetime import timedelta

s3 = boto3.client('s3')
cf_client = boto3.client('cloudfront')

def get_last_check_in_date(url):
    with urllib.request.urlopen(url) as url:
        data = json.loads(url.read().decode())
        return data[0]['commit']['committer']['date']
    
def get_presigned_url(bucket, key):
    response = s3.generate_presigned_url('get_object',
                                         Params={'Bucket': bucket,
                                                 'Key': key},
                                         ExpiresIn=3600)
    return response

def create_payload(dead_man_bucket, dead_man_key, threshold, last_check_in_date):
    time_until_threshold = (last_check_in_date + timedelta(days=threshold)) - datetime.now(timezone.utc)
    threshold_breached = time_until_threshold.total_seconds() <= 0
    presigned_url = get_presigned_url(dead_man_bucket, dead_man_key) if threshold_breached else None
    payload = {
        'lastCheckIn': last_check_in_date.isoformat(),
        'downloadUrl': presigned_url,
        'remainingSeconds': time_until_threshold.total_seconds() if not(threshold_breached) else 0
    }
    return payload
    
def lambda_handler(event, context):
    web_site_bucket = os.environ['WEB_SITE_BUCKET']
    dead_man_bucket = os.environ['DEAD_MAN_BUCKET']
    dead_man_key = os.environ['DEAD_MAN_KEY']
    days_threshold = int(os.environ['DAYS_THRESHOLD'])
    cf_distribution_id = os.environ['CLOUDFRONT_DISTRIBUTION_ID']

    check_in_date = datetime.strptime(get_last_check_in_date('https://api.github.com/repos/adamkilpatrick/trail-notes/commits'), "%Y-%m-%dT%H:%M:%SZ")
    aware_check_in_date = check_in_date.replace(tzinfo=ZoneInfo("UTC"))
    
    payload = create_payload(dead_man_bucket, dead_man_key, days_threshold, aware_check_in_date)
    s3.put_object(
        Bucket=web_site_bucket,
        Key='status/deadMan.json',
        Body=json.dumps(payload),
        ContentType='application/json'
    )
    cf_client.create_invalidation(
        DistributionId=cf_distribution_id,
        InvalidationBatch={
            'Paths': {
                'Quantity': 1,
                'Items': ['/status/deadMan.json']
            },
            'CallerReference': str(time.time()) 
        }
    )

def main():
    check_in_date = datetime.strptime(get_last_check_in_date('https://api.github.com/repos/adamkilpatrick/trail-notes/commits'), "%Y-%m-%dT%H:%M:%SZ")
    aware_check_in_date = check_in_date.replace(tzinfo=ZoneInfo("UTC"))
    payload = create_payload('foo', 'bar', 2, aware_check_in_date)
    print(json.dumps(payload))

if __name__ == "__main__":
    main()