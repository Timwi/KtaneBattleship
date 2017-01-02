using System;
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

    void Start()
    {
        Debug.Log("[Battleship] Started");

        //CreateSquares();
    }

    private void CreateSquares()
    {
        const float x1 = -.07f;
        const float x2 = .03f;
        const float y1 = -.07f;
        const float y2 = .03f;

        const float oDepth = .015f;
        const float iDepth = .014f;

        const float padding = .003f;
        const float spacing = .002f;

        var w = (x2 - x1 - 2 * padding - 5 * spacing) / 6;
        var h = (y2 - y1 - 2 * padding - 5 * spacing) / 6;
        var children = new KMSelectable[8 * 7];

        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                var cellName = ((char) ('A' + x)).ToString() + ((char) ('1' + y));

                var ix1 = x1 + padding + (w + spacing) * x;
                var iy1 = y1 + padding + (h + spacing) * y;
                var ix2 = ix1 + w;
                var iy2 = iy1 + h;

                var go = new GameObject { name = string.Format("Square {0}", cellName) };
                go.transform.parent = TheParent.transform;
                go.AddComponent<MeshFilter>().mesh = SquareMesh;
                go.AddComponent<MeshRenderer>().material = SquareMaterial;
                go.transform.localPosition = new Vector3((ix1 + ix2) / 2, iDepth, -(iy1 + iy2) / 2);
                go.transform.localScale = new Vector3(w, w, w);
                go.transform.localEulerAngles = new Vector3(0, 0, 0);

                var kms = go.AddComponent<KMSelectable>();
                kms.Parent = TheParent;

                var highlight = new GameObject { name = string.Format("Highlight {0}", cellName) };
                highlight.transform.parent = go.transform;
                var hl = highlight.AddComponent<KMHighlightable>();
                hl.HighlightScale = new Vector3(1, 1, 1);
                kms.Highlight = hl;

                highlight.transform.localPosition = new Vector3(0, 0, 0);
                highlight.transform.localScale = new Vector3(1, 1.1f, 1);
                highlight.transform.localEulerAngles = new Vector3(0, 0, 0);

                highlight.AddComponent<MeshFilter>().mesh = HighlightMesh;

                children[8 * (y + 1) + x + 1] = kms;
            }

        for (int x = 0; x < 6; x++)
        {
            var colName = ((char) ('A' + x)).ToString();
            var ix1 = x1 + padding + (w + spacing) * x;
            var iy1 = y1 + padding + (h + spacing) * -1;
            var ix2 = ix1 + w;
            var iy2 = iy1 + h;

            var go = new GameObject { name = string.Format("Col {0}", colName) };
            go.transform.parent = TheParent.transform;
            var txt = go.AddComponent<TextMesh>();
            txt.font = TextFont;
            txt.text = colName;
            txt.anchor = TextAnchor.MiddleCenter;
            txt.fontSize = 64;
            txt.color = Color.black;

            go.GetComponent<MeshRenderer>().material = TextMaterial;
            go.transform.localPosition = new Vector3((ix1 + ix2) / 2, oDepth + .00001f, -(iy1 + iy2) / 2);
            go.transform.localScale = new Vector3(w / 10, w / 10, w / 10);
            go.transform.localEulerAngles = new Vector3(90, 0, 0);

            var kms = go.AddComponent<KMSelectable>();
            kms.Parent = TheParent;

            var highlight = new GameObject { name = string.Format("Col highlight {0}", colName) };
            highlight.transform.parent = go.transform;
            var hl = highlight.AddComponent<KMHighlightable>();
            hl.HighlightScale = new Vector3(1, 1, 1);
            kms.Highlight = hl;

            highlight.transform.localPosition = new Vector3(0, 0, 0);
            highlight.transform.localScale = new Vector3(1, 1, 1);
            highlight.transform.localEulerAngles = new Vector3(-90, 0, 0);

            highlight.AddComponent<MeshFilter>().mesh = TextHighlightMesh;

            children[x + 1] = kms;
        }

        for (int y = 0; y < 6; y++)
        {
            var rowName = ((char) ('1' + y)).ToString();
            var ix1 = x1 + padding + (w + spacing) * -1;
            var iy1 = y1 + padding + (h + spacing) * y;
            var ix2 = ix1 + w;
            var iy2 = iy1 + h;

            var go = new GameObject { name = string.Format("Row {0}", rowName) };
            go.transform.parent = TheParent.transform;
            var txt = go.AddComponent<TextMesh>();
            txt.font = TextFont;
            txt.text = rowName;
            txt.anchor = TextAnchor.MiddleCenter;
            txt.fontSize = 64;
            txt.color = Color.black;

            go.GetComponent<MeshRenderer>().material = TextMaterial;
            go.transform.localPosition = new Vector3((ix1 + ix2) / 2, oDepth + .00001f, -(iy1 + iy2) / 2);
            go.transform.localScale = new Vector3(w / 10, w / 10, w / 10);
            go.transform.localEulerAngles = new Vector3(90, 0, 0);

            var kms = go.AddComponent<KMSelectable>();
            kms.Parent = TheParent;

            var highlight = new GameObject { name = string.Format("Row highlight {0}", rowName) };
            highlight.transform.parent = go.transform;
            var hl = highlight.AddComponent<KMHighlightable>();
            hl.HighlightScale = new Vector3(1, 1, 1);
            kms.Highlight = hl;

            highlight.transform.localPosition = new Vector3(0, 0, 0);
            highlight.transform.localScale = new Vector3(1, 1, 1);
            highlight.transform.localEulerAngles = new Vector3(-90, 0, 0);

            highlight.AddComponent<MeshFilter>().mesh = TextHighlightMesh;

            children[8 * (y + 1)] = kms;
        }

        TheParent.Children = children.ToArray();
        TheParent.ChildRowLength = 8;
    }

    void ActivateModule()
    {
        Debug.Log("[Battleship] Activated");
    }
}
