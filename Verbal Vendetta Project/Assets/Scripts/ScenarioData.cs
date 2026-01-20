using System;
using System.Collections.Generic;

// These classes allow Unity to turn the Gemini JSON string into actual C# objects.


[Serializable]
public class ScenarioData
{
    // Victim Info (Flattened for easier access)
    public string victim_name;
    public string victim_occupation;
    public string victim_discovery_details; // The "Hook" for the player's first questions

    // Case Info
    public string murder_weapon;
    public string murder_location;

    // Suspect List
    public List<SuspectData> suspects;
}

[Serializable]
public class SuspectData
{
    public string name;
    public string personality;
    public string alibi_statement;
    public string minor_secret; // Can be null if they are completely honest

    // Key: Suspect Name, Value: Rumor/Hunch about that person
    public Dictionary<string, string> rumors;

    // Logical Flags for the Triple-Filter
    // The Game Logic uses these to verify the "Intersection of Guilt"
    public bool is_in_group_a; // The Liars (Killer + 1 Red Herring)
    public bool is_in_group_b; // The Motivated (Killer + 1-2 others)
    public bool is_in_group_c; // The Capable (Killer + 1-2 others with weapon access)

    public bool is_killer; // Must be true only for the person in Groups A, B, and C
}