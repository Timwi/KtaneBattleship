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
    public GameObject Icon;
    public Texture[] Icons;

    public KMSelectable RadarButton, WaterButton, TorpedoButton;
    public MeshRenderer RadarButtonObject, WaterButtonObject, TorpedoButtonObject;
    public TextMesh RadarLabel, WaterLabel, TorpedoLabel;
    public Material RadarDark, WaterDark, TorpedoDark, RadarLight, WaterLight, TorpedoLight;
    public TextMesh[] ShipLengthLabels;

    private TextMesh[] _columns = new TextMesh[5];
    private TextMesh[] _rows = new TextMesh[5];

    private KMSelectable _selectedButton;
    private Transform _selectedButtonObject;
    private Coroutine _buttonCoroutine;
    private bool _isSolved, _isExploded;
    private int[] _safeLocations;
    private bool[][] _solution;
    private GameObject[][] _graphics;
    private string[][] _graphicNames;
    private bool[][] _revealed;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int r = 0; r < 5; r++)
        {
            SetRowHandler(MainSelectable.transform.Find("Row " + (char) ('1' + r)), r);
            for (int c = 0; c < 5; c++)
                SetCellHandler(MainSelectable.transform.Find("Square " + (char) ('A' + c) + (char) ('1' + r)).GetComponent<KMSelectable>(), c, r);
        }
        for (int c = 0; c < 5; c++)
            SetColHandler(MainSelectable.transform.Find("Col " + (char) ('A' + c)), c);

        _isSolved = false;
        _isExploded = false;
        _safeLocations = null;
        _solution = null;
        _revealed = Ut.NewArray<bool>(5, 5);

        SetButtonHandler(RadarButton, RadarButtonObject, RadarLabel, RadarLight);
        SetButtonHandler(WaterButton, WaterButtonObject, WaterLabel, WaterLight);
        SetButtonHandler(TorpedoButton, TorpedoButtonObject, TorpedoLabel, TorpedoLight);

        Module.OnActivate = GeneratePuzzle;
        Bomb.OnBombExploded = delegate { _isExploded = true; };
    }

    private void SetRowHandler(Transform obj, int row)
    {
        var sel = obj.GetComponent<KMSelectable>();
        _rows[row] = obj.GetComponent<TextMesh>();

        sel.OnInteract = delegate
        {
            sel.AddInteractionPunch(.25f);
            if (_safeLocations == null || _isSolved || Enumerable.Range(0, 5).All(c => _revealed[c][row]))
                return false;

            if (Enumerable.Range(0, 5).Any(c => !_revealed[c][row] && _solution[c][row] == true))
            {
                var col = Enumerable.Range(0, 5).First(c => !_revealed[c][row] && _solution[c][row] == true);
                Debug.LogFormat("[Battleship #{2}] Used Water on Row {1}, but there is an unrevealed ship piece at {0}{1}.", (char) ('A' + col), (char) ('1' + row), _moduleId);
                Module.HandleStrike();
            }
            else
            {
                for (int col = 0; col < 5; col++)
                    _revealed[col][row] = true;
                UpdateRevealedGraphics();
                Audio.PlaySoundAtTransform("Splash" + Rnd.Range(1, 9), MainSelectable.transform);
            }

            CheckSolved();
            return false;
        };
    }

    private void SetColHandler(Transform obj, int col)
    {
        var sel = obj.GetComponent<KMSelectable>();
        _columns[col] = obj.GetComponent<TextMesh>();

        sel.OnInteract = delegate
        {
            sel.AddInteractionPunch(.25f);
            if (_safeLocations == null || _isSolved || Enumerable.Range(0, 5).All(r => _revealed[col][r]))
                return false;

            if (Enumerable.Range(0, 5).Any(r => !_revealed[col][r] && _solution[col][r] == true))
            {
                var row = Enumerable.Range(0, 5).First(r => !_revealed[col][r] && _solution[col][r] == true);
                Debug.LogFormat("[Battleship #{2}] Used Water on Column {0}, but there is an unrevealed ship piece at {0}{1}.", (char) ('A' + col), (char) ('1' + row), _moduleId);
                Module.HandleStrike();
            }
            else
            {
                for (int row = 0; row < 5; row++)
                    _revealed[col][row] = true;
                UpdateRevealedGraphics();
                Audio.PlaySoundAtTransform("Splash" + Rnd.Range(1, 9), MainSelectable.transform);
            }

            CheckSolved();
            return false;
        };
    }

    private void SetCellHandler(KMSelectable sel, int col, int row)
    {
        sel.OnInteract = delegate
        {
            sel.AddInteractionPunch(.25f);
            if (_selectedButton == null || _safeLocations == null || _isSolved || _revealed[col][row])
                return false;

            Reveal(col, row);

            if (_selectedButton == RadarButton && !_safeLocations.Contains(col + 5 * row))
            {
                Debug.LogFormat("[Battleship #{2}] Used Radar on {0}{1}, which is not a safe location.", (char) ('A' + col), (char) ('1' + row), _moduleId);
                Module.HandleStrike();
            }
            else if (_selectedButton == WaterButton && _solution[col][row] != false)
            {
                Debug.LogFormat("[Battleship #{2}] Used Water on {0}{1}, which is not water.", (char) ('A' + col), (char) ('1' + row), _moduleId);
                Module.HandleStrike();
            }
            else if (_selectedButton == TorpedoButton && _solution[col][row] != true)
            {
                Debug.LogFormat("[Battleship #{2}] Used Torpedo on {0}{1}, which is not a ship piece.", (char) ('A' + col), (char) ('1' + row), _moduleId);
                Module.HandleStrike();
            }

            CheckSolved();
            return false;
        };
    }

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        var buttons = new List<KMSelectable>();
        string parameters = null;

        if (command.StartsWith("radar ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(RadarButton);
            parameters = command.Substring(6).ToUpperInvariant();
        }
        else if (command.StartsWith("scan ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(RadarButton);
            parameters = command.Substring(5).ToUpperInvariant();
        }
        else if (command.StartsWith("miss ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(WaterButton);
            parameters = command.Substring(5).ToUpperInvariant();
        }
        else if (command.StartsWith("water ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(WaterButton);
            parameters = command.Substring(6).ToUpperInvariant();
        }
        else if (command.StartsWith("hit ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(TorpedoButton);
            parameters = command.Substring(4).ToUpperInvariant();
        }
        else if (command.StartsWith("torpedo ", StringComparison.InvariantCultureIgnoreCase))
        {
            buttons.Add(TorpedoButton);
            parameters = command.Substring(8).ToUpperInvariant();
        }
        else if (command.StartsWith("row ", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var r in command.Substring(4))
            {
                if (r == ' ')
                    continue;
                if (r == '1' || r == '2' || r == '3' || r == '4' || r == '5')
                    buttons.Add(MainSelectable.transform.Find("Row " + r).GetComponent<KMSelectable>());
                else
                    // Bail out completely if any one character is invalid.
                    return null;
            }
        }
        else if (command.StartsWith("col ", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var c in command.Substring(4).ToUpperInvariant())
            {
                if (c == ' ')
                    continue;
                if (c == 'A' || c == 'B' || c == 'C' || c == 'D' || c == 'E')
                    buttons.Add(MainSelectable.transform.Find("Col " + c).GetComponent<KMSelectable>());
                else
                    // Bail out completely if any one character is invalid.
                    return null;
            }
        }

        if (parameters != null)
        {
            foreach (var cell in parameters.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Bail out completely if any one location is invalid.
                if (cell.Length != 2 || !"ABCDE".Contains(cell[0]) || !"12345".Contains(cell[1]))
                    return null;
                buttons.Add(MainSelectable.transform.Find("Square " + cell).GetComponent<KMSelectable>());
            }
        }

        return buttons.Count > 0 ? buttons.ToArray() : null;
    }

    private void CheckSolved()
    {
        if (Enumerable.Range(0, 5).All(r => Enumerable.Range(0, 5).All(c => _revealed[c][r] || !_solution[c][r])))
            // If a strike caused the bomb to explode, give Bomb.OnBombExploded a chance
            // to trigger so that we can avoid calling HandlePass() after the bomb has blown up.
            StartCoroutine(Solved());
    }

    private IEnumerator Solved()
    {
        _isSolved = true;
        yield return null;
        if (!_isExploded)
        {
            Module.HandlePass();
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                    _revealed[i][j] = true;
            UpdateRevealedGraphics();
            StartCoroutine(MoveButtons(_selectedButtonObject, null, null, null));
        }
    }

    private void Reveal(int col, int row)
    {
        if (_revealed[col][row])
            return;
        _revealed[col][row] = true;
        UpdateRevealedGraphics(col, row);
        Audio.PlaySoundAtTransform(_solution[col][row] ? "Expl" + Rnd.Range(1, 16) : "Splash" + Rnd.Range(1, 9), MainSelectable.transform);
    }

    private void UpdateRevealedGraphics(int? clickedCol = null, int? clickedRow = null)
    {
        for (int c = 0; c < 5; c++)
            for (int r = 0; r < 5; r++)
            {
                if (!_revealed[c][r])
                    continue;

                var waterAbove = r == 0 || (_revealed[c][r - 1] && !_solution[c][r - 1]);
                var waterBelow = r == 4 || (_revealed[c][r + 1] && !_solution[c][r + 1]);
                var waterLeft = c == 0 || (_revealed[c - 1][r] && !_solution[c - 1][r]);
                var waterRight = c == 4 || (_revealed[c + 1][r] && !_solution[c + 1][r]);

                var shipAbove = r == 0 || (_revealed[c][r - 1] && _solution[c][r - 1]);
                var shipBelow = r == 4 || (_revealed[c][r + 1] && _solution[c][r + 1]);
                var shipLeft = c == 0 || (_revealed[c - 1][r] && _solution[c - 1][r]);
                var shipRight = c == 4 || (_revealed[c + 1][r] && _solution[c + 1][r]);

                SetGraphic(c, r,
                    !_solution[c][r] ? "SqWater" :
                    waterAbove && waterBelow && waterLeft && waterRight ? "SqShipA" :
                    waterAbove && waterLeft && waterBelow && shipRight ? "SqShipL" :
                    waterAbove && waterRight && waterBelow && shipLeft ? "SqShipR" :
                    waterLeft && waterAbove && waterRight && shipBelow ? "SqShipT" :
                    waterLeft && waterBelow && waterRight && shipAbove ? "SqShipB" :
                    waterBelow && waterAbove && shipLeft && shipRight ? "SqShipF" :
                    waterLeft && waterRight && shipAbove && shipBelow ? "SqShipF" : "SqShip",
                    clickedCol == c && clickedRow == r ? 0 : c + r);
            }
    }

    private void SetButtonHandler(KMSelectable button, MeshRenderer buttonObject, TextMesh label, Material lightMaterial)
    {
        button.OnInteract = delegate
        {
            if (_selectedButton == button || _buttonCoroutine != null || _safeLocations == null || _isSolved)
                return false;

            button.AddInteractionPunch(.01f);
            Audio.PlaySoundAtTransform("ButtonDown", buttonObject.transform);

            _buttonCoroutine = StartCoroutine(MoveButtons(_selectedButtonObject, buttonObject, label, lightMaterial));
            _selectedButton = button;
            _selectedButtonObject = buttonObject.transform;
            return false;
        };
    }

    private IEnumerator MoveButtons(Transform prev, MeshRenderer next, TextMesh nextLabel, Material lightMaterial)
    {
        const float down = -.07f;
        const float up = 0f;
        const int iterations = 10;

        RadarLabel.color = Color.black;
        WaterLabel.color = Color.black;
        TorpedoLabel.color = Color.black;

        RadarButtonObject.material = RadarDark;
        WaterButtonObject.material = WaterDark;
        TorpedoButtonObject.material = TorpedoDark;

        for (var i = 0; i <= iterations; i++)
        {
            if (prev != null)
                prev.localPosition = new Vector3(prev.localPosition.x, down + (up - down) / iterations * i, prev.localPosition.z);
            if (next != null)
                next.transform.localPosition = new Vector3(next.transform.localPosition.x, up - (up - down) / iterations * i * 1.25f, next.transform.localPosition.z);
            yield return null;
        }

        if (next != null)
        {
            nextLabel.color = Color.white;
            next.material = lightMaterial;

            const int iterations2 = 5;
            const float down2 = -.07f * 1.25f;
            for (var i = 0; i <= iterations2; i++)
            {
                next.transform.localPosition = new Vector3(next.transform.localPosition.x, down2 + (down - down2) / iterations2 * i, next.transform.localPosition.z);
                yield return null;
            }
        }

        _buttonCoroutine = null;
    }

    void GeneratePuzzle()
    {
        const int size = 5;

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
        Debug.LogFormat("[Battleship #{1}] Safe locations: {0}", _safeLocations.Select(h => "" + (char) ('A' + h % size) + (char) ('1' + h / size)).JoinString(", "), _moduleId);


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
        var nonUnique = 0;
        retry:
        attempts++;
        if (attempts == 1000)
        {
            Debug.LogFormat("[Battleship #{0}] Could not generate puzzle. Giving up.", _moduleId);
            Module.HandlePass();
            return;
        }
        var ships = new[] { Rnd.Range(2, 5), Rnd.Range(2, 4), Rnd.Range(1, 4), Rnd.Range(1, 3), Rnd.Range(0, 2) }.Where(x => x != 0).OrderByDescending(x => x).ToArray();
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

            Debug.LogFormat("[Battleship #{0}] The generated puzzle is impossible. This should never happen!", _moduleId);
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
        {
            nonUnique++;
            goto retry;
        }

        // Found a solution. Now keep searching to see if there’s another, i.e. see whether the puzzle is unique.
        _solution = Ut.NewArray(size, size, (i, j) => grid[i][j] ?? false);
        goto contradiction;

        uniqueSolutionFound:
        Debug.LogFormat("[Battleship #{3}] Ships: {0}\n   {1}\n{2}",
            ships.JoinString(", "),
            Enumerable.Range(0, size).Select(col => colCounts[col].ToString().PadLeft(2)).JoinString(),
            Enumerable.Range(0, size).Select(row => rowCounts[row].ToString().PadLeft(3) + " " + Enumerable.Range(0, size).Select(col => _safeLocations.Contains(col + row * size) ? (_solution[col][row] ? "% " : "• ") : _solution[col][row] ? "# " : "· ").JoinString()).JoinString("\n"),
            _moduleId);

        for (int i = 0; i < size; i++)
        {
            _rows[i].text = rowCounts[i] == 0 ? "o" : rowCounts[i].ToString();
            _columns[i].text = colCounts[i] == 0 ? "o" : colCounts[i].ToString();
        }
        for (int i = 0; i < 4; i++)
        {
            var cnt = ships.Count(s => s == i + 1);
            ShipLengthLabels[i].text = cnt == 0 ? "" : string.Format(i < 2 ? "×{0}" : "{0}×", cnt);
        }
    }

    private void SetGraphic(int col, int row, string name, int delay)
    {
        if (_graphics == null)
            _graphics = Ut.NewArray<GameObject>(5, 5);
        if (_graphicNames == null)
            _graphicNames = Ut.NewArray<string>(5, 5);

        if (_graphicNames[col][row] == name)
            return;
        _graphicNames[col][row] = name;

        StartCoroutine(SetGraphicIterator(col, row, name, delay / 30f));
    }

    IEnumerator SetGraphicIterator(int col, int row, string name, float delay)
    {
        yield return new WaitForSeconds(delay);

        GameObject graphic;
        if (_graphics[col][row] == null)
        {
            graphic = _graphics[col][row] = Instantiate(Icon);
            graphic.name = "Icon " + (char) ('A' + col) + (char) ('1' + row);
            graphic.transform.parent = MainSelectable.transform;
            graphic.transform.localEulerAngles = new Vector3(0, 180, 0);
        }
        else
            graphic = _graphics[col][row];

        var mr = graphic.GetComponent<MeshRenderer>();
        mr.material.mainTexture = Icons.First(t => t.name == name);
        mr.material.shader = Shader.Find("Unlit/Transparent");

        var elapsed = 0f;
        const float duration = .1f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            var t = duration - elapsed;
            graphic.transform.localPosition = new Vector3(col * 0.0192f - 0.0584f, 0.01401f + (.1f * t), 0.0584f - row * 0.0192f);
            graphic.transform.localScale = new Vector3(0.00172f, 0.00172f, 0.00172f) * (100 + 50 * t) / 100f;
        }
        graphic.transform.localPosition = new Vector3(col * 0.0192f - 0.0584f, 0.01401f, 0.0584f - row * 0.0192f);
        graphic.transform.localScale = new Vector3(0.00172f, 0.00172f, 0.00172f);
    }
}
