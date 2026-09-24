Audit this RimWorld C# mod for performance problems.

Focus on performance issues that can materially affect TPS, frame time, GC pressure, or scalability with large colonies/maps. Do not make speculative micro-optimizations unless they have a plausible measurable benefit.

Inspect the entire C# codebase for:

* Tick(), TickRare(), TickLong(), MapComponentTick(), WorldComponentTick(), HediffComp.PostTick(), CompTick(), and other frequently executed code.

* Harmony patches on hot vanilla methods.

* Full Pawn, Thing, Map, Cell, Def, Hediff, Job, Region, or WorldPawn scans.

* Nested loops and O(N²) or worse behavior.

* LINQ, ToList(), ToArray(), lambdas, closures, boxing, temporary collections, strings, and other allocations in hot paths.

* Repeated GetComp(), Def lookups, Hediff searches, reflection, and other repeated lookups that could be cached.

* Expensive spatial operations such as GenClosest, GenRadial, CellFinder, Region/Room queries, reachability checks, and pathfinding.

* Expensive WorkGiver, JobGiver, ThinkNode, JobDriver, and Toil logic.

* Per-pawn or per-thing polling that could instead be event-driven.

* Cases where many individual Things/Pawns perform the same work that could be centralized in a MapComponent/GameComponent.

* Missing use of IsHashIntervalTick(), TickRare(), TickLong(), dirty flags, cached state, registration lists, or event-driven updates.

* Caches with incorrect or missing invalidation on Spawn, Despawn, Destroy, death, map removal, load, new game, or settings changes.

* OnGUI/UI code performing allocations, searches, or expensive calculations every frame.

* Repeated Graphic, Material, Texture, or Mesh lookup/construction.

* Excessive logging or exceptions occurring inside frequently executed code.

* Save/load code serializing unnecessary caches or large derived datasets.

* Static collections/references that can retain Maps, Pawns, or Things after they should be released.

* Operations that scale poorly when pawn, enemy, entity, building, or map counts become large.

For every issue found, report:

1. File and method.

2. The exact problematic code or pattern.

3. How frequently it can execute.

4. What it scales with.

5. Approximate algorithmic complexity where meaningful.

6. Why it can affect RimWorld performance.

7. Severity: Critical / High / Medium / Low.

8. A concrete optimization.

9. Any behavioral or compatibility risk introduced by the optimization.

Prioritize issues approximately as:

Critical:\
Can cause severe TPS degradation, runaway processing, massive allocation, or pathological scaling.

High:\
Located in a hot path and scales significantly with pawn/thing/entity count.

Medium:\
Measurable repeated work or allocation, but unlikely to dominate normal gameplay.

Low:\
Micro-optimization or code that executes infrequently.

Also look for multiplicative behavior. For example, a small operation inside Pawn.Tick or a Harmony patch may become expensive because it executes for every pawn every tick.

Prefer reducing call frequency and algorithmic work over micro-optimizing individual instructions.

Do not modify behavior merely for theoretical performance gains. Clearly distinguish confirmed performance problems from possible optimization opportunities.

At the end, produce a prioritized performance report listing the highest-impact changes first.
