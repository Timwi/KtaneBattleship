using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Battleship;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Battleship
/// Created by Timwi
/// </summary>
public class BattleshipModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public KMSelectable TheParent;
    public Mesh SquareMesh, HighlightMesh;
    public Material SquareMaterial;

    public Font TextFont;
    public Mesh TextHighlightMesh;
    public Material TextMaterial;

    public KMSelectable[] Buttons;
    public Transform[] ButtonObjects;

    private KMSelectable[] _grid = new KMSelectable[36];
    private KMSelectable[] _columns = new KMSelectable[6];
    private KMSelectable[] _rows = new KMSelectable[6];

    private KMSelectable _selectedButton;
    private Transform _selectedButtonObject;
    private Coroutine _buttonCoroutine;
    private bool _isSolved;

    void Start()
    {
        Debug.Log("[Battleship] Started");

        for (int row = 0; row < 6; row++)
        {
            _rows[row] = TheParent.transform.FindChild("Row " + (char) ('1' + row)).GetComponent<KMSelectable>();
            for (int col = 0; col < 6; col++)
                _grid[row * 6 + col] = TheParent.transform.FindChild("Square " + (char) ('A' + col) + (char) ('1' + row)).GetComponent<KMSelectable>();
        }
        for (int col = 0; col < 6; col++)
            _columns[col] = TheParent.transform.FindChild("Col " + (char) ('A' + col)).GetComponent<KMSelectable>();

        _isSolved = false;

        for (int i = 0; i < Buttons.Length; i++)
        {
            var j = i;
            Buttons[i].OnInteract = delegate { HandleButton(Buttons[j], ButtonObjects[j]); return false; };
        }
    }

    private void HandleButton(KMSelectable button, Transform buttonObject)
    {
        if (_selectedButton == button || _buttonCoroutine != null || _isSolved)
            return;

        button.AddInteractionPunch();
        Audio.PlaySoundAtTransform("ButtonDown", buttonObject);

        _buttonCoroutine = StartCoroutine(moveButtons(_selectedButtonObject, buttonObject));
        _selectedButton = button;
        _selectedButtonObject = buttonObject;
    }

    private IEnumerator moveButtons(Transform prev, Transform next)
    {
        const float start = -.07f;
        const float end = 0f;
        const int iterations = 10;

        for (var i = 0; i <= iterations; i++)
        {
            if (prev != null)
                prev.localPosition = new Vector3(prev.localPosition.x, start + (end - start) / iterations * i, prev.localPosition.z);
            next.localPosition = new Vector3(next.localPosition.x, end - (end - start) / iterations * i, next.localPosition.z);
            yield return null;
        }

        _buttonCoroutine = null;
    }

    void ActivateModule()
    {
        Debug.Log("[Battleship] Activated");
    }
}
