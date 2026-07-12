#!/bin/bash

PORT=4318

echo "Listening for OTLP/HTTP JSON on port $PORT..."

while true; do
  # 1. Listen for one connection and save the raw stream to a temporary file
  nc -l -p "$PORT" -q 1 > /tmp/otel_raw.txt 2>/dev/null
  
  # 2. Extract only the JSON body (lines after the blank HTTP header delimiter)
  #    and format it nicely using jq
  if [ -s /tmp/otel_raw.txt ]; then
    echo "--- New Telemetry Payload Received ---"
    awk 'BEGIN {RS="\r?\n\r?\n"; ORS=""} NR==2 {print}' /tmp/otel_raw.txt | jq '.' 2>/dev/null
    echo -e "\n---------------------------------------\n"
  fi
  
  # 3. Clean up the temp file
  rm -f /tmp/otel_raw.txt
done

