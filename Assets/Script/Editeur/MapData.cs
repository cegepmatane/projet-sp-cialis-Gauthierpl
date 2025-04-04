using System;
using System.Collections.Generic;
using UnityEngine;

// Classe pour encapsuler la liste d'objets pour JsonUtility
[Serializable]
public class MapDefinition
{
    // public string mapName; // Optionnel: Pour ajouter un nom plus tard
    public List<MapObjectData> objects = new List<MapObjectData>();
}

[Serializable]
public class MapObjectData
{
    public string prefabId; // Identifiant unique pour le type d'objet (ex: "wood_floor", "cat_tree")
    public Vector3 position;
    public float rotationZ; // On stocke juste la rotation sur l'axe Z pour la 2D
    public Vector2 scale;   // Utiliser scale.x pour la largeur, scale.y pour la hauteur
}