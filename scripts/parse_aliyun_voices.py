import json

with open("aliyun_voices_raw.json", "r", encoding="utf-8") as f:
    data = json.load(f)

# Navigate the JSON structure based on Bailian response
try:
    voices = data["data"]["DataV2"]["data"]["data"]
except KeyError:
    # If the format is slightly different
    voices = data.get("data", [])

output = []
for item in voices:
    cfg = item.get("ttsVoiceConfig", {})
    voice_id = cfg.get("voice", "")
    name = cfg.get("name", "")
    gender_raw = cfg.get("gender", "")
    gender = "女" if "女" in gender_raw or "Female" in gender_raw else "男" if "男" in gender_raw or "Male" in gender_raw else ""
    
    language_raw = cfg.get("language", "")
    if "多语" in language_raw or "中英" in language_raw or "English" in language_raw:
        lang = "多语言"
    else:
        lang = "中文"
        
    sample_url = cfg.get("illustrationAudio", "")
    
    if voice_id:
        line = f'                new VoiceOption {{ Id = "{voice_id}", Name = "{name} ({gender})", Gender = "{gender}", Language = "{lang}", SampleUrl = "{sample_url}" }},'
        output.append(line)

with open("aliyun_voices_parsed.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(output))
print(f"Parsed {len(output)} voices")
