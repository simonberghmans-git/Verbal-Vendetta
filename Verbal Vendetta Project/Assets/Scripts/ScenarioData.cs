using System;
using System.Collections.Generic;
using Newtonsoft.Json; // Added to handle the ToString conversion

// These classes allow Unity to turn the Gemini JSON string into actual C# objects.
[Serializable]
public class ScenarioData
{
    // Victim Info
    public string victim_name;
    public string victim_occupation;
    public string victim_discovery_details;

    // Case Info
    public string murder_weapon;
    public string murder_location;

    // Suspect List
    public List<SuspectData> suspects;

    // --- THE FIX ---
    // This method tells Unity: "When I ask for a string version of this object, 
    // use the JSON library to format all my variables nicely."
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}

[Serializable]
public class SuspectData
{
    public string name;
    public string personality;
    public string alibi_statement;
    public string minor_secret;

    public Dictionary<string, string> rumors;

    // Logical Flags for the Triple-Filter
    public bool is_in_group_a;
    public bool is_in_group_b;
    public bool is_in_group_c;

    public bool is_killer;

    // We add it here too, so you can print individual suspects if needed.
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}