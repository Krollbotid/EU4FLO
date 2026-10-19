namespace EU4FLO.Optimization;
using EU4FLO.Localization;

public sealed class OptimizationRequest
{
    public HashSet<int> Universe { get; init; } = new();
    public Dictionary<int, SortedSet<int>> Adjacency { get; init; } = new();
    public HashSet<int> Required  { get; init; } = new();
    public HashSet<int> Preferred { get; init; } = new();
    public HashSet<int> Exceptions { get; init; } = new();
    public List<FortGroup> Groups { get; init; } = new();
    public AdjacencyMode Mode { get; init; }
    public HashSet<int> Candidates { get; init; } = new();
}

public sealed class OptimizationResult
{
    public HashSet<int> Forts { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool CoverageComplete { get; set; }
}

public static class DominatingSetSolver
{
    public static OptimizationResult Solve(OptimizationRequest req)
    {
        var result = new OptimizationResult();
        var solution = result.Forts;

        var inGroup = new Dictionary<int, FortGroup>();
        foreach (var g in req.Groups)
            foreach (var p in g.Provinces)
                if (!inGroup.ContainsKey(p)) inGroup[p] = g;

        var covered = new HashSet<int>();

        void ApplyNode(int pid)
        {
            if (!solution.Add(pid)) return;
            covered.Add(pid);
            if (req.Adjacency.TryGetValue(pid, out var nbrs))
                foreach (var n in nbrs) covered.Add(n);
        }

        bool IsAdjacencyAllowed(int pid)
        {
            if (req.Mode == AdjacencyMode.AnyAdjacent) return true;
            if (!req.Adjacency.TryGetValue(pid, out var nbrs)) return true;
            foreach (var n in nbrs)
            {
                if (!solution.Contains(n)) continue;
                if (req.Mode == AdjacencyMode.NoAdjacent) return false;
                // Homebrew: разрешено, если хотя бы одна из пары — исключительная
                if (req.Exceptions.Contains(pid) || req.Exceptions.Contains(n)) continue;
                return false;
            }
            return true;
        }

        // Универсум
        var universe = req.Universe.Count > 0
            ? new HashSet<int>(req.Universe)
            : new HashSet<int>(req.Candidates);

        // Phase A: обязательные
        foreach (var pid in req.Required)
            if (req.Candidates.Contains(pid)) ApplyNode(pid);

        // Phase B: жадное покрытие
        var uncovered = new HashSet<int>(universe.Where(p => !covered.Contains(p)));
        while (uncovered.Count > 0)
        {
            int best = -1, bestScore = 0;
            bool bestPreferred = false, bestUnderfilled = false;

            foreach (var c in req.Candidates)
            {
                if (solution.Contains(c)) continue;
                if (!IsAdjacencyAllowed(c)) continue;

                int score = uncovered.Contains(c) ? 1 : 0;
                if (req.Adjacency.TryGetValue(c, out var nbrs))
                    foreach (var n in nbrs)
                        if (uncovered.Contains(n)) score++;

                if (score == 0) continue;

                bool pref = req.Preferred.Contains(c);
                bool under = inGroup.TryGetValue(c, out var gg) &&
                             CountGroup(solution, gg) < gg.MinForts;

                bool better =
                    best == -1 ||
                    score > bestScore ||
                    (score == bestScore && under && !bestUnderfilled) ||
                    (score == bestScore && under == bestUnderfilled && pref && !bestPreferred);

                if (better)
                {
                    best = c; bestScore = score;
                    bestPreferred = pref; bestUnderfilled = under;
                }
            }

            if (best == -1)
            {
                result.Warnings.Add(Loc.T("solver.warn.uncovered", uncovered.Count));
                result.CoverageComplete = false;
                break;
            }

            ApplyNode(best);
            uncovered.RemoveWhere(p => covered.Contains(p));
        }
        if (uncovered.Count == 0) result.CoverageComplete = true;

        // Phase C: групповые минимумы
        foreach (var g in req.Groups)
        {
            while (CountGroup(solution, g) < g.MinForts)
            {
                int pick = -1;
                bool pickPref = false;
                foreach (var p in g.Provinces)
                {
                    if (!req.Candidates.Contains(p)) continue;
                    if (solution.Contains(p)) continue;
                    if (!IsAdjacencyAllowed(p)) continue;
                    bool pref = req.Preferred.Contains(p);
                    if (pick == -1 || (pref && !pickPref)) { pick = p; pickPref = pref; }
                }
                if (pick == -1)
                {
                    result.Warnings.Add(Loc.T("solver.warn.group_min", g.Name, g.MinForts));
                    break;
                }
                ApplyNode(pick);
            }
        }

        // Phase D: прунинг избыточных
        int Degree(int pid) => req.Adjacency.TryGetValue(pid, out var s) ? s.Count : 0;
        var sorted = solution.OrderByDescending(Degree).ToList();
        foreach (var p in sorted)
        {
            if (req.Required.Contains(p)) continue;
            if (!solution.Contains(p)) continue;

            solution.Remove(p);
            bool ok = true;

            foreach (var g in req.Groups)
                if (g.Provinces.Contains(p) && CountGroup(solution, g) < g.MinForts)
                { ok = false; break; }

            if (ok)
            {
                var affected = new HashSet<int> { p };
                if (req.Adjacency.TryGetValue(p, out var nbrs))
                    foreach (var n in nbrs) affected.Add(n);

                foreach (var u in affected)
                {
                    if (!universe.Contains(u)) continue;
                    bool cov = solution.Contains(u) ||
                               (req.Adjacency.TryGetValue(u, out var s) && s.Any(solution.Contains));
                    if (!cov) { ok = false; break; }
                }
            }

            if (!ok) solution.Add(p);
        }

        // После прунинга проверим группы и при необходимости добьём
        foreach (var g in req.Groups)
        {
            while (CountGroup(solution, g) < g.MinForts)
            {
                int pick = -1;
                foreach (var p in g.Provinces)
                {
                    if (!req.Candidates.Contains(p)) continue;
                    if (solution.Contains(p)) continue;
                    if (!IsAdjacencyAllowed(p)) continue;
                    pick = p; break;
                }
                if (pick == -1) break;
                ApplyNode(pick);
            }
        }

        return result;
    }

    private static int CountGroup(HashSet<int> solution, FortGroup g)
    {
        int n = 0;
        foreach (var p in g.Provinces) if (solution.Contains(p)) n++;
        return n;
    }
}