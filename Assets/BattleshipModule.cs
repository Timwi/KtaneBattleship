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

    public KMSelectable MainSelectable;
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
    private int[] _safeLocations;
    private bool[][] _solution;
    private bool[][] _revealed;

    void Start()
    {
        for (int row = 0; row < 6; row++)
        {
            _rows[row] = MainSelectable.transform.FindChild("Row " + (char) ('1' + row)).GetComponent<KMSelectable>();
            for (int col = 0; col < 6; col++)
                _grid[row * 6 + col] = MainSelectable.transform.FindChild("Square " + (char) ('A' + col) + (char) ('1' + row)).GetComponent<KMSelectable>();
        }
        for (int col = 0; col < 6; col++)
            _columns[col] = MainSelectable.transform.FindChild("Col " + (char) ('A' + col)).GetComponent<KMSelectable>();

        _isSolved = false;

        for (int i = 0; i < Buttons.Length; i++)
        {
            var j = i;
            Buttons[i].OnInteract = delegate { HandleButton(Buttons[j], ButtonObjects[j]); return false; };
        }

        Module.OnActivate = ActivateModule;
    }

    private void HandleButton(KMSelectable button, Transform buttonObject)
    {
        if (_selectedButton == button || _buttonCoroutine != null || _safeLocations == null || _isSolved)
            return;

        button.AddInteractionPunch();
        Audio.PlaySoundAtTransform("ButtonDown", buttonObject);

        _buttonCoroutine = StartCoroutine(moveButtons(_selectedButtonObject, buttonObject));
        _selectedButton = button;
        _selectedButtonObject = buttonObject;
    }

    private IEnumerator moveButtons(Transform prev, Transform next)
    {
        const float down = -.07f;
        const float up = 0f;
        const int iterations = 10;

        for (var i = 0; i <= iterations; i++)
        {
            if (prev != null)
                prev.localPosition = new Vector3(prev.localPosition.x, down + (up - down) / iterations * i, prev.localPosition.z);
            next.localPosition = new Vector3(next.localPosition.x, up - (up - down) / iterations * i * 1.25f, next.localPosition.z);
            yield return null;
        }

        const int iterations2 = 5;
        const float down2 = -.07f * 1.25f;
        for (var i = 0; i <= iterations2; i++)
        {
            next.localPosition = new Vector3(next.localPosition.x, down2 + (down - down2) / iterations2 * i, next.localPosition.z);
            yield return null;
        }

        _buttonCoroutine = null;
    }

    void ActivateModule()
    {
        const int size = 6;

        // What are the safe locations?
        var safeColumns = Bomb.GetSerialNumberLetters().Select(ch => (ch - 'A') % size).ToList();
        var safeRows = Bomb.GetSerialNumberNumbers().Select(num => (num % size + size - 1) % size).ToList();
        while (safeColumns.Count > safeRows.Count)
            safeColumns.RemoveAt(safeRows.Count);
        while (safeRows.Count > safeColumns.Count)
            safeRows.RemoveAt(safeColumns.Count);
        safeColumns.Add((Bomb.GetPortCount() + size - 1) % size);
        safeRows.Add((Bomb.GetIndicators().Count() + Bomb.GetBatteryCount() + size - 1) % size);
        _safeLocations = Enumerable.Range(0, safeColumns.Count).Select(i => safeColumns[i] + size * safeRows[i]).ToArray();
        Debug.LogFormat("[Battleship] Safe locations: {0}", _safeLocations.Select(h => "" + (char) ('A' + h % size) + (char) ('1' + h / size)).JoinString(", "));


        // ═══════════════════════════════════════════════════════════════════════════════════════
        // ALGORITHM to generate BATTLESHIP PUZZLES with UNIQUE SOLUTIONS
        // ═══════════════════════════════════════════════════════════════════════════════════════
        // The following algorithm makes heavy use of goto. Because many readers of this code may find this difficult to follow,
        // here is a summary of the algorithm with only the gotos in place.
        // ───────────────────────────────────────────────────────────────────────
        //  retry:
        //  Initialize everything to initial values.
        //  Generate a random arrangement of ships, calculate rowCounts and colCounts, then empty the grid again.
        //
        //  nextIter:
        //  If the grid is full,
        //      goto tentativeSolution;
        //
        //  Deduce cells from obvious heuristics.
        //  If a deduction can be made,
        //      goto nextIter;
        //  If a column or row becomes impossible,
        //      goto contradiction;
        //
        //  No obvious deduction: Try a hypothesis. Place a Ship in an undeduced location and push it onto a Stack
        //  goto nextIter;
        //
        //  contradiction:
        //  If the Stack is empty and one solution has been found,
        //      goto uniqueSolutionFound;
        //
        //  If the Stack is empty, the puzzle is impossible, which shouldn’t happen because we generated a valid one
        //      goto retry;
        //
        //  Pop the most recent hypothesis off the Stack and place Water there.
        //  goto nextIter;
        //
        //  tentativeSolution:
        //  If no hypothesis had been made, the puzzle is too trivial.
        //      goto retry;
        //
        //  Found a tentative solution. Check that it’s valid by counting all the ships.
        //  If there are too many of any particular length of ship,
        //      goto contradiction;
        //  If any ships are unaccounted for (this should never happen because the previous if would have to trigger as well),
        //      goto contradiction;
        //
        //  Found a valid solution.
        //  If a previous solution had already been found, the puzzle is not unique.
        //      goto retry;
        //
        //  Otherwise, remember this solution and keep going to see if there is a second solution.
        //  goto contradiction;
        //
        //  uniqueSolutionFound:
        //  Done!
        // ───────────────────────────────────────────────────────────────────────


        // Keep retrying to generate a puzzle until we find one that has a unique solution but isn’t trivial to solve.
        var attempts = 0;
        retry:
        attempts++;
        if (attempts == 1000)
        {
            Debug.LogFormat("[Battleship] Giving up.");
            return;
        }
        var ships = new[] { Rnd.Range(4, 6), Rnd.Range(3, 5), Rnd.Range(2, 4), Rnd.Range(1, 4), Rnd.Range(1, 3), 1 }.OrderByDescending(x => x).ToArray();
        //var ships = new[] { 4, 4, 2, 1, 1, 1 }.OrderByDescending(x => x).ToArray();
        Debug.LogFormat("[Battleship] Attempt #{0}. Ships: {1}", attempts, ships.JoinString(", "));
        var anyHypothesis = false;
        var grid = Ut.NewArray(size, size, (x, y) => (bool?) null);
        _solution = null;

        // Place the ships randomly in the grid.
        var availableShips = ships.ToList();
        while (availableShips.Count > 0)
        {
            var ix = Rnd.Range(0, availableShips.Count);
            var shipLen = availableShips[ix];
            availableShips.RemoveAt(ix);
            var positions = Enumerable.Range(0, size * size * 2).Select(i =>
            {
                var horiz = (i & 1) == 1;
                var x = (i >> 1) % size;
                var y = (i >> 1) / size;
                if (horiz && x + shipLen >= size || !horiz && y + shipLen >= size)
                    return null;
                for (int j = 0; j < shipLen; j++)
                    if (grid[horiz ? x + j : x][horiz ? y : y + j] != null)
                        return null;
                return new { X = x, Y = y, Horiz = horiz };
            }).Where(inf => inf != null).ToArray();

            // There is no place to put this ship. No bother, just restart from the beginning.
            if (positions.Length == 0)
                goto retry;

            var pos = positions.PickRandom();
            for (int j = -1; j <= shipLen; j++)
            {
                var ps = pos.Horiz ? pos.X + j : pos.Y + j;
                if (ps >= 0 && ps < size)
                {
                    if ((pos.Horiz ? pos.Y : pos.X) > 0)
                        grid[pos.Horiz ? pos.X + j : pos.X - 1][pos.Horiz ? pos.Y - 1 : pos.Y + j] = false;
                    grid[pos.Horiz ? pos.X + j : pos.X][pos.Horiz ? pos.Y : pos.Y + j] = j >= 0 && j < shipLen;
                    if ((pos.Horiz ? pos.Y : pos.X) < size - 1)
                        grid[pos.Horiz ? pos.X + j : pos.X + 1][pos.Horiz ? pos.Y + 1 : pos.Y + j] = false;
                }
            }
        }

        var rowCounts = Enumerable.Range(0, size).Select(row => Enumerable.Range(0, size).Count(col => grid[col][row] == true)).ToArray();
        var colCounts = Enumerable.Range(0, size).Select(col => Enumerable.Range(0, size).Count(row => grid[col][row] == true)).ToArray();

        // Now empty the grid again and see if we can deduce the solution uniquely.
        grid = Ut.NewArray(size, size, (x, y) => _safeLocations.Contains(x + y * size) ? grid[x][y] : null);

        var rowsDone = new bool[size];
        var colsDone = new bool[size];
        var hypotheses = new[] { new { X = 0, Y = 0, Grid = (bool?[][]) null, RowsDone = (bool[]) null, ColsDone = (bool[]) null } }.ToStack();
        hypotheses.Pop();

        nextIter:
        if (rowsDone.All(b => b) && colsDone.All(b => b))
            goto tentativeSolution;

        // Diagonal from a true is a false
        for (int c = 0; c < size; c++)
            for (int r = 0; r < size; r++)
                if (grid[c][r] == true)
                {
                    if (r > 0 && c > 0)
                        grid[c - 1][r - 1] = false;
                    if (r > 0 && c < size - 1)
                        grid[c + 1][r - 1] = false;
                    if (r < size - 1 && c > 0)
                        grid[c - 1][r + 1] = false;
                    if (r < size - 1 && c < size - 1)
                        grid[c + 1][r + 1] = false;
                }

        var anyDeduced = false;

        // Check if a row can be filled in unambiguously
        for (int r = 0; r < size; r++)
            if (!rowsDone[r])
            {
                var cnt = Enumerable.Range(0, size).Count(c => grid[c][r] != false);
                if (cnt < rowCounts[r])
                    goto contradiction;

                if (cnt == rowCounts[r])
                {
                    for (int c = 0; c < size; c++)
                        if (grid[c][r] == null)
                            grid[c][r] = true;
                    rowsDone[r] = true;
                    anyDeduced = true;
                }

                cnt = Enumerable.Range(0, size).Count(c => grid[c][r] == true);
                if (cnt > rowCounts[r])
                    goto contradiction;

                if (cnt == rowCounts[r])
                {
                    for (int c = 0; c < size; c++)
                        if (grid[c][r] == null)
                            grid[c][r] = false;
                    rowsDone[r] = true;
                    anyDeduced = true;
                }
            }

        // Check if a column can be filled in unambiguously
        for (int c = 0; c < size; c++)
            if (!colsDone[c])
            {
                var cnt = Enumerable.Range(0, size).Count(r => grid[c][r] != false);
                if (cnt < colCounts[c])
                    goto contradiction;

                if (cnt == colCounts[c])
                {
                    for (int r = 0; r < size; r++)
                        if (grid[c][r] == null)
                            grid[c][r] = true;
                    colsDone[c] = true;
                    anyDeduced = true;
                }

                cnt = Enumerable.Range(0, size).Count(r => grid[c][r] == true);
                if (cnt > colCounts[c])
                    goto contradiction;

                if (cnt == colCounts[c])
                {
                    for (int r = 0; r < size; r++)
                        if (grid[c][r] == null)
                            grid[c][r] = false;
                    colsDone[c] = true;
                    anyDeduced = true;
                }
            }

        if (anyDeduced)
            goto nextIter;

        // No obvious deduction. Explore a hypothesis by placing a ship in the first undeduced space
        anyHypothesis = true;
        var unfinishedCol = Array.IndexOf(colsDone, false);
        var unfinishedRow = Array.IndexOf(grid[unfinishedCol], null);
        hypotheses.Push(new { X = unfinishedCol, Y = unfinishedRow, Grid = Ut.NewArray(size, size, (x, y) => grid[x][y]), RowsDone = (bool[]) rowsDone.Clone(), ColsDone = (bool[]) colsDone.Clone() });
        grid[unfinishedCol][unfinishedRow] = true;
        goto nextIter;

        contradiction:
        if (hypotheses.Count == 0)
        {
            if (_solution != null)
                goto uniqueSolutionFound;

            Debug.LogFormat("[Battleship] The generated puzzle is impossible. This should never happen!");
            goto retry;
        }

        // Backtrack to the last hypothesis and place water instead
        var prevHypo = hypotheses.Pop();
        grid = prevHypo.Grid;
        rowsDone = prevHypo.RowsDone;
        colsDone = prevHypo.ColsDone;
        grid[prevHypo.X][prevHypo.Y] = false;
        goto nextIter;

        tentativeSolution:

        // If the puzzle was deduced entirely through trivial deductions, it’s too easy.
        if (!anyHypothesis)
            goto retry;

        // Check that the tentative solution is correct by counting all the ships.
        var unaccountedFor = ships.OrderByDescending(x => x).ToList();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                int? thisLen = null;
                if (grid[x][y] == true && (x == 0 || grid[x - 1][y] == false) && (x == size - 1 || grid[x + 1][y] == false) && (y == 0 || grid[x][y - 1] == false) && (y == size - 1 || grid[x][y + 1] == false))
                    thisLen = 1;
                if (thisLen == null && grid[x][y] == true && (x == 0 || grid[x - 1][y] == false))
                {
                    var len = 0;
                    while (x + len < size && grid[x + len][y] == true)
                        len++;
                    if (len > 1 && (x + len == size || grid[x + len][y] == false))
                        thisLen = len;
                }
                if (thisLen == null && grid[x][y] == true && (y == 0 || grid[x][y - 1] == false))
                {
                    var len = 0;
                    while (y + len < size && grid[x][y + len] == true)
                        len++;
                    if (len > 1 && (y + len == size || grid[x][y + len] == false))
                        thisLen = len;
                }
                // Are there too many ships of this length?
                if (thisLen != null && !unaccountedFor.Remove(thisLen.Value))
                    goto contradiction;
            }

        // Is there a ship length unaccounted for? (This should never happen because if it is so, then another ship length must have too many, so the previous check should have caught it.)
        if (unaccountedFor.Count > 0)
            goto contradiction;

        // Found a valid solution. Have we found a solution before? If so, the puzzle is not unique.
        if (_solution != null)
            goto retry;

        // Found a solution. Now keep searching to see if there’s another, i.e. see whether the puzzle is unique.
        _solution = Ut.NewArray(size, size, (i, j) => grid[i][j] ?? false);
        goto contradiction;

        uniqueSolutionFound:
        Debug.LogFormat("[Battlehsip] Solution is:\n   {0}\n{1}",
            Enumerable.Range(0, size).Select(col => colCounts[col].ToString().PadLeft(2)).JoinString(),
            Enumerable.Range(0, size).Select(row => rowCounts[row].ToString().PadLeft(3) + " " + Enumerable.Range(0, size).Select(col => _safeLocations.Contains(col + row * size) ? (_solution[col][row] ? "% " : "• ") : _solution[col][row] ? "# " : "· ").JoinString()).JoinString("\n"));

        for (int i = 0; i < size; i++)
        {
            _rows[i].GetComponent<TextMesh>().text = rowCounts[i] == 0 ? "o" : rowCounts[i].ToString();
            _columns[i].GetComponent<TextMesh>().text = colCounts[i] == 0 ? "o" : colCounts[i].ToString();
        }
    }
}
