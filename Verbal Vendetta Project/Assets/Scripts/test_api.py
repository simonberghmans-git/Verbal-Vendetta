import urllib.request
import urllib.error
import urllib.parse
import json
import sys

def check_models(api_key):
    url = f"https://generativelanguage.googleapis.com/v1alpha/models?key={api_key}"
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read().decode())
            
            print("Models supporting bidiGenerateContent:")
            for m in data.get('models', []):
                if 'bidiGenerateContent' in m.get('supportedGenerationMethods', []):
                    print(f"- {m['name']}")
                    
    except urllib.error.HTTPError as e:
        print(f"HTTP Error: {e.code} - {e.reason}")
        error_msg = e.read().decode()
        print(f"Error Details: {error_msg}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Please provide API Key")
        sys.exit(1)
    
    check_models(sys.argv[1])
