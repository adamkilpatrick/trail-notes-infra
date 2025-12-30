import urllib.request, json, os, boto3
from datetime import datetime, timezone
from zoneinfo import ZoneInfo


WEATHER_API_URL = 'https://api.open-meteo.com/v1/forecast?latitude={}&longitude={}&daily=temperature_2m_max,temperature_2m_min&hourly=temperature_2m&timezone=America%2FNew_York&forecast_days=1&temperature_unit=fahrenheit'

def get_last_check_in_date(url):
    with urllib.request.urlopen(url) as url:
        data = json.loads(url.read().decode())
        return data[0]['commit']['committer']['date']

def get_last_check_in_loc(url):
    with urllib.request.urlopen(url) as url:
        data = json.loads(url.read().decode())
        return data['path'][-1]['loc']

def get_weather(lat, long):
    with urllib.request.urlopen(WEATHER_API_URL.format(lat, long)) as url:
        data = json.loads(url.read().decode())
        return {
            'highTemp': data['daily']['temperature_2m_max'],
            'lowTemp': data['daily']['temperature_2m_min']
        }

def lambda_handler(event, context):
    # TODO cram these into env variables
    date = get_last_check_in_date('https://api.github.com/repos/adamkilpatrick/trail-notes/commits')
    loc = get_last_check_in_loc('https://trail.snakeha.us/paths/test.json')
    weather = get_weather(loc[0], loc[1])
    payload = {
        'date': date,
        'loc': loc,
        'weather': weather,
        'timestamp': datetime.utcnow().isoformat()
    }
    
    s3_bucket = os.environ['S3_BUCKET']
    s3_key = f"status/{datetime.now(ZoneInfo('America/New_York')).strftime('%Y-%m-%d')}.json"
    s3_key_latest = f"status/latest.json"
    
    s3 = boto3.client('s3')
    s3.put_object(
        Bucket=s3_bucket,
        Key=s3_key,
        Body=json.dumps(payload),
        ContentType='application/json'
    )
    s3.put_object(
        Bucket=s3_bucket,
        Key=s3_key_latest,
        Body=json.dumps(payload),
        ContentType='application/json'
    )
    
    return {
        'statusCode': 200,
        'body': json.dumps({'message': f'Status check saved to s3://{s3_bucket}/{s3_key}'})
    }

def main():
    date = get_last_check_in_date('https://api.github.com/repos/adamkilpatrick/trail-notes/commits')
    loc = get_last_check_in_loc('https://trail.snakeha.us/paths/test.json')
    weather = get_weather(loc[0], loc[1])
    payload = {
        'date': date,
        'loc': loc,
        'weather': weather
    }
    print(json.dumps(payload))

if __name__ == "__main__":
    main()