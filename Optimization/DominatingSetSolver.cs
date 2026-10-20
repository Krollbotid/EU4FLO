using Google.OrTools.Sat;
using EU4FLO.Localization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EU4FLO.Optimization;

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
    public static OptimizationResult Solve(OptimizationRequest req, int timeLimitSeconds = 10)
    {
        var result = new OptimizationResult();
        try
        {
            var model = new CpModel();

            // 1. Переменные: 1 если ставим форт, 0 если нет
            var x = new Dictionary<int, BoolVar>();
            foreach (var p in req.Candidates)
                x[p] = model.NewBoolVar($"x_{p}");

            // 2. Цель: минимизировать количество фортов, но отдавать предпочтение Preferred
            // Мы умножаем количество фортов на 1000, чтобы 1 сэкономленный форт всегда был важнее, 
            // чем любые бонусы за Preferred (которых не может быть больше 1000).
            var objectiveTerms = x.Values.Select(v => (LinearExpr)v).ToList();
            var objective = LinearExpr.Sum(objectiveTerms) * 1000;
            
            foreach (var p in req.Preferred)
            {
                if (x.TryGetValue(p, out var pVar))
                    objective -= pVar; // Вычитаем 1, если провинция Preferred выбрана
            }
            model.Minimize(objective);

            // 3. Покрытие: каждая провинция из Universe должна иметь форт сама или у соседа
            foreach (var u in req.Universe)
            {
                var terms = new List<LinearExpr>();
                if (x.TryGetValue(u, out var uVar)) terms.Add(uVar);
                
                if (req.Adjacency.TryGetValue(u, out var nbrs))
                {
                    foreach (var v in nbrs)
                        if (x.TryGetValue(v, out var vVar)) terms.Add(vVar);
                }

                if (terms.Count > 0)
                    model.Add(LinearExpr.Sum(terms) >= 1);
                else
                {
                    // Если провинцию невозможно покрыть (нет форта и все соседи запрещены)
                    result.Warnings.Add(Loc.T("solver.warn.uncovered", 1));
                    return result;
                }
            }

            // 4. Обязательные крепости
            foreach (var p in req.Required)
            {
                if (x.TryGetValue(p, out var pVar)) 
                    model.Add(pVar == 1);
                else
                {
                    result.Warnings.Add($"Required fort {p} is not in candidates (maybe water/wasteland or outside coverage).");
                    return result;
                }
            }

            // 5. Режимы соседства (NoAdjacent / Homebrew)
            foreach (var kvp in req.Adjacency)
            {
                int u = kvp.Key;
                if (!x.ContainsKey(u)) continue;
                
                foreach (var v in kvp.Value)
                {
                    if (u < v && x.ContainsKey(v)) // Проверяем каждую пару только один раз
                    {
                        if (req.Mode == AdjacencyMode.NoAdjacent)
                        {
                            model.Add(x[u] + x[v] <= 1);
                        }
                        else if (req.Mode == AdjacencyMode.Homebrew)
                        {
                            if (!req.Exceptions.Contains(u) && !req.Exceptions.Contains(v))
                                model.Add(x[u] + x[v] <= 1);
                        }
                    }
                }
            }

            // 6. Групповые ограничения
            foreach (var g in req.Groups)
            {
                var gTerms = g.Provinces.Where(p => x.ContainsKey(p)).Select(p => (LinearExpr)x[p]).ToList();
                if (gTerms.Count < g.MinForts)
                {
                    result.Warnings.Add(Loc.T("solver.warn.group_min", g.Name, g.MinForts));
                    return result;
                }
                model.Add(LinearExpr.Sum(gTerms) >= g.MinForts);
            }

            // 7. Запуск солвера
            var solver = new CpSolver();
            solver.StringParameters = $"max_time_in_seconds:{timeLimitSeconds} num_search_workers:8"; 
            var status = solver.Solve(model);

            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                foreach (var p in req.Candidates)
                    if (solver.Value(x[p]) == 1)
                        result.Forts.Add(p);
                
                result.CoverageComplete = true;
                
                if (status == CpSolverStatus.Feasible)
                    result.Warnings.Add("Solver reached time limit. The found solution might not be mathematically optimal.");
            }
            else
            {
                result.Warnings.Add($"Solver failed with status: {status}. The constraints might be impossible to satisfy.");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Solver exception: {ex.Message}");
        }

        return result;
    }
}