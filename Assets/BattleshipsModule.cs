using System;
using System.Collections.Generic;
using System.Linq;
using Battleships;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Battleships
/// Created by Timwi
/// </summary>
public class BattleshipsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    void Start()
    {
        Debug.Log("[Battleships] Started");
    }

    void ActivateModule()
    {
        Debug.Log("[Battleships] Activated");
    }
}
