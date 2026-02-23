import asyncio
import websockets
import json
import sys

async def test_websocket(api_key):
    version = "v1beta"
    model = "models/gemini-2.5-flash-native-audio-preview-12-2025"
    url = f"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.{version}.GenerativeService.BidiGenerateContent?key={api_key}"
    
    systemPrompt = """You are roleplaying as Marcus.
        RULES: 
        1. Stay in character. Respond to the detective's real-time spoken queries.
        2. Keep answers concise (1-3 sentences).
        
        CRITICAL: In your internal thinking step, you must plan your response. Then, when generating audio, you MUST speak EXACTLY word-for-word what you wrote in your thought process. Do not improvise or deviate from the text you just wrote.
        """

    setup_msg = {
        "setup": {
            "model": model,
            "generationConfig": {
                "responseModalities": ["AUDIO"],
                "speechConfig": {
                    "voiceConfig": {
                        "prebuiltVoiceConfig": {"voiceName": "Puck"}
                    }
                }
            },
            "systemInstruction": {
                "parts": [{"text": systemPrompt}],
                "role": "user"
            }
        }
    }
    
    client_msg = {
        "clientContent": {
            "turns": [
                {
                    "parts": [{"text": "Hello! Where were you last night?"}],
                    "role": "user"
                }
            ],
            "turnComplete": True
        }
    }
    
    try:
        async with websockets.connect(url) as ws:
            await ws.send(json.dumps(setup_msg))
            await ws.recv()
            await ws.send(json.dumps(client_msg))
            
            full_text = ""
            for _ in range(15):
                res = await ws.recv()
                data = json.loads(res)
                if 'serverContent' in data and 'modelTurn' in data['serverContent']:
                    parts = data['serverContent']['modelTurn']['parts']
                    for part in parts:
                        if 'text' in part:
                            full_text += part['text']
                if 'serverContent' in data and data['serverContent'].get('turnComplete'):
                    print(f"--- FULL THOUGHT ENTIRE BLOCK ---\n{full_text}")
                    break
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    asyncio.run(test_websocket(sys.argv[1]))
