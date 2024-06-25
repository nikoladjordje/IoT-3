const mqtt = require('mqtt');
const WebSocket = require('ws');
const http = require('http');
const fs = require('fs');
const path = require('path');

// Environment variables
const MQTT_BROKER_URL = 'mqtt://host.docker.internal:1883';
const MQTT_TOPIC = 'sensor-data';
const WEBSOCKET_PORT = 8080;
const HTTP_PORT = 3000;

// Create an MQTT client
const mqttClient = mqtt.connect(MQTT_BROKER_URL);

// Create a WebSocket server
const wss = new WebSocket.Server({ port: WEBSOCKET_PORT });

// WebSocket connection event
wss.on('connection', ws => {
  console.log('WebSocket client connected');

  // Send a welcome message to the client
  ws.send(JSON.stringify({ message: 'Connected to Command Microservice' }));
});

// MQTT connection event
mqttClient.on('connect', () => {
  console.log(`Connected to MQTT broker at ${MQTT_BROKER_URL}`);
  mqttClient.subscribe(MQTT_TOPIC, (err) => {
    if (err) {
      console.error(`Failed to subscribe to MQTT topic ${MQTT_TOPIC}:`, err);
    } else {
      console.log(`Subscribed to MQTT topic ${MQTT_TOPIC}`);
    }
  });
});

// MQTT message event
mqttClient.on('message', (topic, message) => {
  if (topic === MQTT_TOPIC) {
    console.log(`Received message from MQTT topic ${MQTT_TOPIC}: ${message.toString()}`);

    // Broadcast the message to all connected WebSocket clients
    wss.clients.forEach(client => {
      if (client.readyState === WebSocket.OPEN) { // Check for WebSocket.OPEN
        client.send(message.toString());
      }
    });
  }
});

// Create an HTTP server to serve index.html
const server = http.createServer((req, res) => {
  // Serve index.html file
  if (req.url === '/' || req.url === '/index.html') {
    const indexPath = path.join(__dirname, 'index.html');
    fs.readFile(indexPath, (err, content) => {
      if (err) {
        res.writeHead(500);
        res.end(`Error loading index.html: ${err}`);
      } else {
        res.writeHead(200, { 'Content-Type': 'text/html' });
        res.end(content, 'utf-8');
      }
    });
  } else {
    // Handle 404 - Not Found
    res.writeHead(404);
    res.end('Not Found');
  }
});

// Listen on specified HTTP_PORT
server.listen(HTTP_PORT, () => {
  console.log(`HTTP server is running on http://localhost:${HTTP_PORT}`);
});
