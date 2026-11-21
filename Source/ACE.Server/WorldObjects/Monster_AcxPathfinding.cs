using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Pathfinding;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;

using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: indoor pathfinding for monsters, ported from ClassicACE's routing (TryRoute / StartRoute /
    /// ContinueRoute / RetryRoute / EndRoute) and fitted to ACE's own monster movement.
    ///
    /// When the 'pathfinding' server bool is on, a monster in a dungeon that has no clear path to its target
    /// (melee: its body can't slide straight to the target; archer in range: no line of sight) follows a navmesh route
    /// around the walls, one corner at a time, and hands back to ACE's normal chase or attack as soon as it has a clear
    /// path. A monster that keeps getting stuck steps away from the nearest wall and re-plans, and after three stuck
    /// legs gives up for a while and falls back to ACE's own stuck handling.
    ///
    /// When the bool is off, AcxPathfindingTick returns false before doing anything and ACE's monster AI runs unchanged.
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// How often a chasing monster without a route checks whether it needs one
        /// </summary>
        private const double AcxRouteCheckInterval = 0.5;   // was 1.0 (JJ 2026-11-21: olthoi "very slow and bad")

        /// <summary>
        /// Pause before looking for a route again after none was useful (unreachable target, or the way is straight)
        /// </summary>
        private const double AcxNoRouteBackoff = 2.0;   // was 5.0: each one froze a monster for 5 s

        /// <summary>
        /// Pause after a route was abandoned because the monster kept getting stuck
        /// </summary>
        private const double AcxStuckBackoff = 10.0;

        /// <summary>
        /// How often a routing monster checks whether it can go straight for its target now
        /// </summary>
        private const double AcxClearPathCheckInterval = 0.6;

        /// <summary>
        /// Re-plan when the target has moved this far since the route was planned, at most this often
        /// </summary>
        private const float AcxReplanDistance = 4.0f;
        private const double AcxReplanInterval = 2.0;

        /// <summary>
        /// Route corners closer than this are skipped (ClassicACE)
        /// </summary>
        private const float AcxCornerSkipDistance = 2.0f;

        /// <summary>
        /// Stuck legs before the route is abandoned (ClassicACE's FailedRoutingThreshold)
        /// </summary>
        private const int AcxMaxStuckLegs = 3;

        /// <summary>
        /// A routing monster hands back to ACE's chase only when its target is this close, or has been in clear view for
        /// AcxClearPathHold seconds. Live's counters (2026-11-21, Olthoi Workers in Crumbling Empyrean Mansion) showed 3 routes
        /// in 4 handed back at the first glimpse, and the chase lost the target at the next corner: stop-start movement.
        /// </summary>
        private const float AcxHandBackDistance = 5.0f;
        private const double AcxClearPathHold = 1.0;

        /// <summary>
        /// The next leg starts this close to a corner, so the monster runs on round it instead of stopping on the corner
        /// and turning in place (JJ 2026-11-21 on Preview: "they are having a hard time with adjustments to their paths.. but
        /// once they get on a straight line they are faster than before")
        /// </summary>
        private const float AcxCornerLookAhead = 1.5f;

        private List<Position> acxRoute;
        private int acxRouteIndex;
        private WorldObject acxRouteTarget;
        private Position acxRouteGoal;
        private bool acxRouteRanged;
        private bool acxRouteNudging;
        private int acxRouteStuckLegs;
        private double acxNextRouteCheckTime;
        private double acxNextClearPathCheckTime;
        private double acxLastRouteTickTime;
        private double acxRoutePlanTime;
        private double acxLegStartTime;
        private double acxLegDeadline;
        private double acxClearSince = -1;

        private static double acxNextPathErrorLogTime;

        /// <summary>
        /// Route counters per monster name, logged every 5 minutes while monsters route (JJ 2026-11-21: "Olthoi
        /// pathfinding in dungeons is very slow and bad"): started, handed back on a clear path, route ended, stuck
        /// legs, abandoned after AcxMaxStuckLegs, no useful route (5 s pause), re-plans
        /// </summary>
        private enum AcxRouteStat { Started, ClearPath, Ended, StuckLeg, Abandoned, NoRoute, Replan }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int[]> acxRouteStats = new();
        private static double acxNextRouteStatsLogTime;

        private void AcxCount(AcxRouteStat stat)
        {
            var counts = acxRouteStats.GetOrAdd(Name ?? "?", _ => new int[7]);
            System.Threading.Interlocked.Increment(ref counts[(int)stat]);

            if (Timers.RunningTime < acxNextRouteStatsLogTime)
                return;
            acxNextRouteStatsLogTime = Timers.RunningTime + 300.0;

            foreach (var name in acxRouteStats.Keys)
            {
                if (!acxRouteStats.TryRemove(name, out var c))
                    continue;
                log.Info($"[Pathfinding] stats 5 min {name}: started {c[0]}, clear path {c[1]}, ended {c[2]}, stuck legs {c[3]}, abandoned {c[4]}, no route {c[5]}, replans {c[6]}");
            }
        }

        /// <summary>
        /// TRUE while the monster is following a navmesh route
        /// </summary>
        public bool IsAcxRouting => acxRoute != null;

        /// <summary>
        /// Called by Monster_Tick before ACE's chase / attack logic.
        /// Returns TRUE if pathfinding handled the monster's movement this tick.
        /// </summary>
        public bool AcxPathfindingTick(float targetDist)
        {
            try
            {
                var handled = acxRoute == null ? AcxTryStartRoute(targetDist) : AcxContinueRoute(targetDist);

                if (handled && this is CombatPet combatPet)
                    combatPet.PetCheckMonsters();   // the rest of Monster_Tick is skipped

                return handled;
            }
            catch (Exception ex)
            {
                // a pathfinding fault must never take the landblock down: drop the route and let ACE's AI carry on
                AcxClearRoute();
                acxNextRouteCheckTime = Timers.RunningTime + 30.0;

                if (Timers.RunningTime >= acxNextPathErrorLogTime)
                {
                    acxNextPathErrorLogTime = Timers.RunningTime + 10.0;
                    log.Error($"[Pathfinding] {Name} (0x{Guid}) at {Location?.ToLOCString()}: {ex}");
                }
                return false;
            }
        }

        /// <summary>
        /// Returns TRUE if this monster could use a route to its target right now, and whether it needs line of sight
        /// (an archer in range) or a walkable straight line (anything about to run at its target)
        /// </summary>
        private bool AcxWantsRoute(float targetDist, out bool ranged)
        {
            ranged = false;

            if (MonsterState != State.Awake || AiImmobile || Location == null || !Location.Indoors)
                return false;

            var target = AttackTarget;
            if (target?.Location == null || !target.Location.Indoors || target.Location.Landblock != Location.Landblock)
                return false;

            if (CurrentLandblock == null || !CurrentLandblock.IsDungeon)
                return false;

            if (CurrentAttack == CombatType.Missile)
            {
                // out of range ACE switches the monster to melee; in range it shoots, so it needs line of sight
                if (targetDist > MaxRange)
                    return false;
                ranged = true;
                return true;
            }

            // melee, or a caster out of spell range: ACE runs straight at the target.
            // (In spell range ACE already rerolls projectile spells that have no line of sight.)
            return targetDist > MaxRange;
        }

        private bool AcxHasClearPath(bool ranged)
        {
            return ranged ? IsDirectVisible(AttackTarget) : IsMeleeVisible(AttackTarget);
        }

        /// <summary>
        /// A route only helps if it turns at least one corner: a straight route means ACE's direct chase works,
        /// and whatever failed the clear-path test was not a wall (usually another monster)
        /// </summary>
        private static bool AcxIsUsefulRoute(List<Position> route)
        {
            return route != null && route.Count >= 3;
        }

        private bool AcxTryStartRoute(float targetDist)
        {
            if (Location == null || !Location.Indoors)
                return false;

            var now = Timers.RunningTime;
            if (now < acxNextRouteCheckTime)
                return false;

            if (!Pathfinder.Enabled)
                return false;

            if (!AcxWantsRoute(targetDist, out var ranged))
            {
                acxNextRouteCheckTime = now + AcxRouteCheckInterval;
                return false;
            }

            // attacking, animating or recovering from a cancelled move: try again next tick
            if (!MoveReady())
                return false;

            acxNextRouteCheckTime = now + AcxRouteCheckInterval * ThreadSafeRandom.Next(0.75f, 1.25f);

            // the landblock's mesh loads in the background; until then the monster behaves as in plain ACE
            if (!Pathfinder.RequestMeshes((ushort)Location.Landblock))
                return false;

            if (AcxHasClearPath(ranged))
                return false;

            var route = Pathfinder.FindRouteAnyWidth(Location, AttackTarget.Location);
            if (!AcxIsUsefulRoute(route))
            {
                AcxCount(AcxRouteStat.NoRoute);
                acxNextRouteCheckTime = now + AcxNoRouteBackoff;
                return false;
            }

            AcxCount(AcxRouteStat.Started);

            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - AcxStartRoute: {route.Count} points to {AttackTarget.Name}, ranged={ranged}");

            acxRoute = route;
            acxRouteIndex = 0;
            acxRouteTarget = AttackTarget;
            acxRouteGoal = new Position(AttackTarget.Location);
            acxRouteRanged = ranged;
            acxRouteNudging = false;
            acxRouteStuckLegs = 0;
            acxRoutePlanTime = now;
            acxLastRouteTickTime = now;
            acxNextClearPathCheckTime = now + AcxClearPathCheckInterval;
            acxClearSince = -1;

            AcxStartNextLeg(now);

            return acxRoute != null;
        }

        private bool AcxContinueRoute(float targetDist)
        {
            var now = Timers.RunningTime;

            // the route went stale while Monster_Tick didn't reach us (asleep, returning home, emote busy):
            // whatever ACE started since is in charge
            if (now - acxLastRouteTickTime > 1.0)
            {
                AcxClearRoute();
                return false;
            }
            acxLastRouteTickTime = now;

            if (!Pathfinder.Enabled || AttackTarget == null || AttackTarget != acxRouteTarget || !AcxWantsRoute(targetDist, out var ranged) || ranged != acxRouteRanged)
            {
                AcxEndRoute(true);
                return false;
            }

            // physics step + position broadcast, as Movement() does each tick while chasing
            UpdatePosition();

            if (GetDistanceToTarget() >= MaxChaseRange)
            {
                // as Movement()
                AcxEndRoute(false);
                CancelMoveTo();
                FindNextTarget();
                return true;
            }

            if (now >= acxNextClearPathCheckTime)
            {
                acxNextClearPathCheckTime = now + AcxClearPathCheckInterval;

                if (!AcxHasClearPath(acxRouteRanged))
                    acxClearSince = -1;
                else if (acxClearSince < 0)
                    acxClearSince = now;

                if (acxClearSince >= 0 && (targetDist <= AcxHandBackDistance || now - acxClearSince >= AcxClearPathHold))
                {
                    if (DebugMove)
                        Console.WriteLine($"{Name} ({Guid}) - AcxRoute: clear path to {AttackTarget.Name}");

                    // stop the leg so ACE's chase / attack starts this same tick
                    AcxCount(AcxRouteStat.ClearPath);
                    AcxEndRoute(true);
                    return false;
                }
            }

            if (IsMoving)
            {
                var failProgress = PhysicsObj.MovementManager?.MoveToManager?.FailProgressCount ?? 0;
                if ((failProgress > 0 && now - acxLegStartTime > 1.5) || now > acxLegDeadline)
                {
                    AcxOnLegStuck(now);
                    return true;
                }

                // nearly on the corner: run on to the next one without stopping
                if (!acxRouteNudging && acxRouteIndex > 0 && acxRouteIndex < acxRoute.Count
                    && Location.DistanceTo(acxRoute[acxRouteIndex - 1]) <= AcxCornerLookAhead)
                {
                    acxRouteStuckLegs = 0;
                    AcxStartNextLeg(now);
                    return acxRoute != null;
                }

                // the target moved since the route was planned
                if (!acxRouteNudging && now - acxRoutePlanTime > AcxReplanInterval && AttackTarget.Location.DistanceTo(acxRouteGoal) > AcxReplanDistance)
                    return AcxReplan(now);

                return true;
            }

            // leg done
            if (acxRouteNudging)
            {
                acxRouteNudging = false;
                return AcxReplan(now);
            }

            acxRouteStuckLegs = 0;      // a corner reached clears the stuck count, as a completed move clears ClassicACE's FailedMovementCount
            AcxStartNextLeg(now);
            return acxRoute != null;
        }

        /// <summary>
        /// Moves to the next route corner that isn't right next to the monster; ends the route if there is none
        /// </summary>
        private void AcxStartNextLeg(double now)
        {
            while (acxRouteIndex < acxRoute.Count && Location.DistanceTo(acxRoute[acxRouteIndex]) <= AcxCornerSkipDistance)
                acxRouteIndex++;

            if (acxRouteIndex >= acxRoute.Count)
            {
                if (DebugMove)
                    Console.WriteLine($"{Name} ({Guid}) - AcxRoute: end of route");

                AcxCount(AcxRouteStat.Ended);
                AcxEndRoute(false);
                return;
            }

            AcxMoveToPoint(acxRoute[acxRouteIndex], now);
            acxRouteIndex++;
        }

        private void AcxMoveToPoint(Position point, double now)
        {
            // face the way the monster is going, so nothing turns it at the corner
            var destination = new Position(point);
            destination.Rotate(destination.Pos - Location.Pos);

            // client: run to the position, no final heading
            var motion = GetMoveToPosition(destination, RunRate, 1.0f);
            motion.MoveToParameters.MovementParameters &= ~MovementParams.UseFinalHeading;
            EnqueueBroadcastMotion(motion);

            // server: same MoveTo iterator ACE uses for MoveToHome
            var mvp = new MovementParameters();
            mvp.CanWalk = false;
            PhysicsObj.MoveToPosition(new Physics.Common.Position(destination), mvp);

            // prevent initial snap (as StartTurn)
            PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            IsMoving = true;
            LastMoveTime = Timers.RunningTime;

            acxLegStartTime = now;
            var legTime = Location.DistanceTo(destination) / Math.Max(MoveSpeed, 1.0f);
            acxLegDeadline = now + Math.Min(15.0, legTime * 2.0 + 2.0);
        }

        /// <summary>
        /// ClassicACE's RetryRoute: step away from the nearest wall, then re-plan from there.
        /// After AcxMaxStuckLegs the route is dropped and ACE's own stuck handling runs.
        /// </summary>
        private void AcxOnLegStuck(double now)
        {
            acxRouteStuckLegs++;
            AcxCount(AcxRouteStat.StuckLeg);

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - AcxRoute: stuck ({acxRouteStuckLegs}/{AcxMaxStuckLegs})");

            if (acxRouteStuckLegs >= AcxMaxStuckLegs)
            {
                AcxCount(AcxRouteStat.Abandoned);
                AcxEndRoute(false);
                acxNextRouteCheckTime = now + AcxStuckBackoff;
                CancelMoveTo();     // ACE's handling of a failed chase: stop, reset the attack, pick a target
                return;
            }

            var wall = Pathfinder.GetNearestWallPosition(Location, 1.0f, AgentWidth.Narrow, out _);
            if (wall != null)
            {
                acxRouteNudging = true;
                AcxMoveToPoint(wall.InFrontOf(1.2f), now);
                return;
            }

            AcxReplan(now);
        }

        /// <summary>
        /// Plans a new route from where the monster is. Returns TRUE if it is following one.
        /// </summary>
        private bool AcxReplan(double now)
        {
            AcxCount(AcxRouteStat.Replan);
            var route = Pathfinder.FindRouteAnyWidth(Location, AttackTarget.Location);
            acxRoutePlanTime = now;
            acxRouteGoal = new Position(AttackTarget.Location);

            if (!AcxIsUsefulRoute(route))
            {
                // straight ahead now, or no way at all: hand back to ACE
                AcxEndRoute(true);
                acxNextRouteCheckTime = now + AcxRouteCheckInterval;
                return false;
            }

            acxRoute = route;
            acxRouteIndex = 0;
            AcxStartNextLeg(now);
            return acxRoute != null;
        }

        /// <summary>
        /// Ends the route. With stopLeg, also cancels the move to the current corner,
        /// so ACE's chase or attack can start straight away.
        /// </summary>
        private void AcxEndRoute(bool stopLeg)
        {
            if (stopLeg && PhysicsObj != null && PhysicsObj.IsMovingTo())
            {
                var moveToManager = PhysicsObj.MovementManager.MoveToManager;
                moveToManager.CancelMoveTo(WeenieError.ActionCancelled);
                moveToManager.FailProgressCount = 0;

                EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));
                IsMoving = false;
            }

            AcxClearRoute();
        }

        private void AcxClearRoute()
        {
            acxRoute = null;
            acxRouteIndex = 0;
            acxRouteTarget = null;
            acxRouteGoal = null;
            acxRouteNudging = false;
            acxRouteStuckLegs = 0;
        }
    }
}
