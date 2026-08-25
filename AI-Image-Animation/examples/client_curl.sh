#!/usr/bin/env bash
# AI Image Animation - cURL API Example (Port 3979)

SERVER_URL="http://localhost:3979"

echo "1. Checking Server & GPU VRAM Health..."
curl -s "$SERVER_URL/api/health" | jq .

echo ""
echo "2. Listing Wind Physics Presets..."
curl -s "$SERVER_URL/api/presets" | jq .

echo ""
echo "3. Submitting Animation Task..."
# Replace with your actual base64 image
IMAGE_BASE64="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="

TASK_RESP=$(curl -s -X POST "$SERVER_URL/api/animate/flow" \
  -H "Content-Type: application/json" \
  -d '{
    "image": "'"$IMAGE_BASE64"'",
    "wind_strength": 1.2,
    "wave_frequency": 1.8,
    "turbulence": 0.6,
    "duration_seconds": 3.0,
    "format": "mp4",
    "loop_mode": "seamless_phase"
  }')

echo "Task Response: $TASK_RESP"
TASK_ID=$(echo "$TASK_RESP" | grep -o '"task_id":"[^"]*' | cut -d'"' -f4)

if [ -n "$TASK_ID" ]; then
  echo "Polling task status for: $TASK_ID"
  sleep 2
  curl -s "$SERVER_URL/api/tasks/$TASK_ID" | jq .
fi
