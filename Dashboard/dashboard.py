import asyncio
from nats.aio.client import Client as NATS
from influxdb_client import InfluxDBClient, Point
from influxdb_client.client.write_api import SYNCHRONOUS
from influxdb_client.client.influxdb_client_async import InfluxDBClientAsync
import json

# Configuration
NATS_URL = "nats://nats:4222"
NATS_SUBJECT = "sensor/averages"
INFLUXDB_HOST = 'influxdb'
INFLUXDB_PORT = 8086
INFLUXDB_DB = 'Beehive'
INFLUXDB_TOKEN = 'KzhY2sXzzRcdyYl8KjipChiU8pJdOYAEuEJ0wcVhF6Xf1UIQKa2XB6k-kjsmUdBckazuxL9FQP4l6hUjhzs3tw=='
INFLUXDB_ORG = 'asd'
INFLUXDB_URL = 'http://influxdb:8086'
INFLUXDB_BUCKET = "Beehive" 



async def run():
    nc = NATS()
    

    async def message_handler(msg):
        data = json.loads(msg.data.decode())
        print(f"Received a message: {data}")
        await write_to_db(data)
        
    try:
        await nc.connect(servers=[NATS_URL])
        await nc.subscribe(NATS_SUBJECT, cb=message_handler)
        await nc.flush()
        while True:
            await asyncio.sleep(1)
    finally:
        nc.close()

async def write_to_db(data):
    # Connect to InfluxDB
    async with InfluxDBClientAsync(url=INFLUXDB_URL, token=INFLUXDB_TOKEN, org=INFLUXDB_ORG) as client:
        write_api = client.write_api()
        fields = data['fields']

        point = Point("sensor_data") \
            .tag("device", data["device"]) \
            .field("hive_number", data["hive_number"]) \
            .field("hive_temp", fields["hive temp"]) \
            .field("hive_humidity", fields["hive humidity"]) \
            .field("hive_pressure", fields["hive pressure"]) \
            .field("weather_temp", fields["weather temp"]) \
            .field("weather_humidity", fields["weather humidity"]) \
            .field("weather_pressure", fields["weather pressure"]) \
            .field("wind_speed", fields["wind speed"]) \
            .field("cloud_coverage", fields["cloud coverage"]) \
            .field("rain", fields["rain"]) \
            .field("lat", fields["lat"]) \
            .field("long", fields["long"])

        successfully = await write_api.write(bucket=INFLUXDB_BUCKET, record=point)
        if(successfully):
            print(f"Data written to InfluxDB: {point}")
        else:
            print(f"Data not written")

if __name__ == '__main__':
    loop = asyncio.get_event_loop()
    loop.run_until_complete(run())
    loop.run_forever()
