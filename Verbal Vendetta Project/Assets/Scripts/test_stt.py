import requests
import sys
import json
import base64

def test_stt(api_key):
    model = "gemini-2.0-flash"
    url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"

    # Test payload: basic text
    payload = {
        "contents": [
            {
                "parts": [
                    {"text": "Hello"}
                ]
            }
        ]
    }
    try:
        req = requests.post(url, json=payload)
        print("Text Status:", req.status_code)
        if req.status_code != 200:
            print("Text Response:", req.text)
    except Exception as e:
        print("Error:", e)

    # Let's create a tiny dummy WAV buffer (just silence) to test the endpoint
    import io
    import wave
    buf = io.BytesIO()
    with wave.open(buf, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(24000)
        w.writeframes(b'\x00\x00' * 24000)
    
    b64_wav = base64.b64encode(buf.getvalue()).decode('utf-8')

    # Test payload 2: inline_data with WAV
    payload2 = {
        "contents": [
            {
                "parts": [
                    {"text": "Transcribe the audio:"},
                    {
                        "inline_data": {
                            "mime_type": "audio/wav",
                            "data": b64_wav
                        }
                    }
                ]
            }
        ]
    }
    try:
        req = requests.post(url, json=payload2)
        print("Wav Status:", req.status_code)
        if req.status_code != 200:
            print("Wav Response:", req.text)
    except Exception as e:
        print("Error:", e)

if __name__ == "__main__":
    test_stt(sys.argv[1])
