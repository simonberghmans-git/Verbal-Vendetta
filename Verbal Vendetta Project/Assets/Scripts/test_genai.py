import asyncio
import os
import sys
import logging
import json

logging.basicConfig(level=logging.INFO)

os.environ["GEMINI_API_KEY"] = sys.argv[1]

from google import genai
import websockets.client

# Monkeypatch to catch the payload
original_send = websockets.client.WebSocketClientProtocol.send

async def patched_send(self, message):
    if isinstance(message, str):
        print("\n=== INTERCEPTED SETUP PAYLOAD ===")
        print(message)
        print("=================================\n")
    return await original_send(self, message)

websockets.client.WebSocketClientProtocol.send = patched_send

client = genai.Client()

async def run():
    MODEL = "gemini-2.5-flash-native-audio-preview-12-2025"
    CONFIG = {
        "response_modalities": ["AUDIO"],
        "system_instruction": "You are a helpful assistant.",
    }
    
    try:
        print("Connecting...")
        async with client.aio.live.connect(model=MODEL, config=CONFIG) as session:
            print("Connected and sent setup.")
            await asyncio.sleep(1)
    except Exception as e:
        print(f"Error: {e}")

asyncio.run(run())
