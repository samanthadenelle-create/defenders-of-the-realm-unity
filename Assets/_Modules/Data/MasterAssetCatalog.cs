using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MasterAssetCatalog", menuName = "Defenders/Catalog/Master Asset Catalog")]
public class MasterAssetCatalog : ScriptableObject
{
    [Header("=== Buildings ===")]
    public List<BuildingEntry> buildings = new List<BuildingEntry>();

    [Header("=== Defense Structures ===")]
    public List<DefenseEntry> defensePieces = new List<DefenseEntry>();

    [Header("=== Props & Decoration ===")]
    public List<PropEntry> props = new List<PropEntry>();

    [Header("=== Nature & Environment ===")]
    public List<NatureEntry> nature = new List<NatureEntry>();

    // Helper methods for easy lookup
    public GameObject GetBuilding(string key) => buildings.Find(b => b.key == key)?.prefab;
    public GameObject GetDefense(string key) => defensePieces.Find(d => d.key == key)?.prefab;
    public GameObject GetProp(string key) => props.Find(p => p.key == key)?.prefab;
    public GameObject GetNature(string key) => nature.Find(n => n.key == key)?.prefab;
}

[System.Serializable]
public class BuildingEntry
{
    public string key;           // e.g. "Blacksmith", "Tavern", "Lumbermill"
    public GameObject prefab;
    public string district;      // "North", "East", "South", "West", "Center"
}

[System.Serializable]
public class DefenseEntry
{
    public string key;           // e.g. "Wall_Straight", "Balcony_Straight", "Tower_Corner"
    public GameObject prefab;
    public string type;          // "Wall", "Balcony", "Tower", "Stairs", "Gate"
}

[System.Serializable]
public class PropEntry
{
    public string key;
    public GameObject prefab;
    public string category;      // "Crate", "Vine", "Furniture", etc.
}

[System.Serializable]
public class NatureEntry
{
    public string key;
    public GameObject prefab;
}
