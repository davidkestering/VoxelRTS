﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RTSEngine.Data;
using RTSEngine.Interfaces;
using RTSEngine.Data.Team;
using Microsoft.Xna.Framework;
using RTSEngine.Data.Parsers;

namespace RTSEngine.Controllers {
    public class GameplayController {
        public float TimePlayed {
            get;
            private set;
        }

        // Queue Of Events
        private LinkedList<GameInputEvent> events;

        // Queue Of Commands
        private Queue<DevCommand> commands;

        public GameplayController() {
            TimePlayed = 0f;
            commands = new Queue<DevCommand>();
        }


        // The Update Function
        public void Update(GameState s, float dt) {
            TimePlayed += dt;

            // Input Pass
            ResolveInput(s, dt);
            ApplyInput(s, dt);

            // Logic Pass
            ApplyLogic(s, dt);

            // Physics Pass
            ResolvePhysics(s, dt);

            // Cleanup The State
            Cleanup(s, dt);
        }

        // Input Stage
        private void ResolveInput(GameState s, float dt) {
            // TODO: Use InputControllers From The Teams
            events = new LinkedList<GameInputEvent>();
            foreach(var team in s.Teams) {
                if(team.Input != null) {
                    team.Input.AppendEvents(events);
                }
            }

        }
        private void ApplyInput(GameState s, float dt) {

            GameInputEvent e;
            GameEventType eType;

            while(events.Count > 0) {
                e = events.First();
                eType = e.Action;

                switch(eType) {
                    case GameEventType.Select:
                        SelectEvent selectEvent = e as SelectEvent;
                        selectEvent.Team.Input.Selected = selectEvent.Selected;
                        break;
                    case GameEventType.SetWaypoint:
                        SetWayPointEvent setWaypointEvent = e as SetWayPointEvent;
                        List<IEntity> selected = setWaypointEvent.Team.Input.Selected;
                        if(selected != null && selected.Count > 0) {
                            foreach(var unit in selected) {
                                RTSUnitInstance u = unit as RTSUnitInstance;
                                if(u != null) {
                                    u.MovementController.SetWaypoints(new Vector2[] { setWaypointEvent.Waypoint });
                                }
                            }
                        }
                        break;
                    case GameEventType.SetTarget:
                        SetTargetEvent setTargetEvent = e as SetTargetEvent;
                        selected = setTargetEvent.Team.Input.Selected;
                        RTSSquad squad = new RTSSquad();
                        if(selected != null && selected.Count > 0) {
                            foreach(var unit in selected) {
                                squad.AddCombatant((ICombatEntity)unit);
                                RTSUnitInstance u = unit as RTSUnitInstance;
                                u.Squad.RemoveAll(u2 => u2.Equals(u));
                            }
                        }
                        setTargetEvent.Team.AddSquad(squad);
                        squad.TargettingController = s.Controllers["RTSCS.TargettingController"].CreateInstance() as ITargettingController;
                        squad.TargettingController.Target = setTargetEvent.Target;
                        break;
                    default:
                        throw new Exception("Event does not exist.");
                }
                events.RemoveFirst();
            }
        }

        // Logic Stage
        private void ApplyLogic(GameState s, float dt) {
            // Apply Dev Commands
            int c = commands.Count;
            for(int i = 0; i < c; i++) {
                var comm = commands.Dequeue();
                switch(comm.Type) {
                    case DevCommandType.Spawn:
                        var dcs = comm as DevCommandSpawn;
                        for(int ci = 0; ci < dcs.Count; ci++) {
                            var unit = s.Teams[dcs.TeamIndex].AddUnit(dcs.UnitIndex, new Vector2(dcs.X, dcs.Z));
                            unit.ActionController = s.Controllers[s.Teams[dcs.TeamIndex].UnitData[dcs.UnitIndex].DefaultActionController].CreateInstance() as IActionController;
                            unit.AnimationController = s.Controllers[s.Teams[dcs.TeamIndex].UnitData[dcs.UnitIndex].DefaultAnimationController].CreateInstance() as IAnimationController;
                            unit.MovementController = s.Controllers[s.Teams[dcs.TeamIndex].UnitData[dcs.UnitIndex].DefaultMoveController].CreateInstance() as IMovementController;
                        }
                        break;
                }
            }

            // Find Decisions
            foreach(RTSTeam team in s.Teams) {
                foreach(RTSUnitInstance unit in team.Units) {
                    if(unit.ActionController == null) continue;
                    unit.ActionController.DecideAction(s, dt);
                }
            }

            // Apply Controllers
            foreach(RTSTeam team in s.Teams) {
                foreach(RTSUnitInstance unit in team.Units) {
                    if(unit.ActionController == null) continue;
                    unit.ActionController.ApplyAction(s, dt);
                }
            }
        }

        // Physics Stage
        private void ResolvePhysics(GameState s, float dt) {

            // Initialize hash grid
            HashGrid hashGrid = new HashGrid(s.Map.Width, s.Map.Depth, 2);

            // Move Geometry To The Unit's Location and hash into the grid
            foreach(RTSTeam team in s.Teams) {
                foreach(RTSUnitInstance unit in team.Units) {
                    unit.CollisionGeometry.Center = unit.GridPosition;
                    hashGrid.AddObject(unit);
                }
            }

            // Use hash grid to perform collision using 3 by 3 grid around the geometry
            foreach(Point p in hashGrid.Active) {
                hashGrid.HandleGridCollision(p.X, p.Y);
                hashGrid.HandleGridCollision(p.X, p.Y, -1, -1);
                hashGrid.HandleGridCollision(p.X, p.Y, -1, 0);
                hashGrid.HandleGridCollision(p.X, p.Y, -1, 1);
                hashGrid.HandleGridCollision(p.X, p.Y, 0, -1);
                hashGrid.HandleGridCollision(p.X, p.Y, 0, 1);
                hashGrid.HandleGridCollision(p.X, p.Y, 1, -1);
                hashGrid.HandleGridCollision(p.X, p.Y, 1, 0);
                hashGrid.HandleGridCollision(p.X, p.Y, 1, 1);
            }

            // Move Unit's Location To The Geometry After Heightmap Collision
            foreach(RTSTeam team in s.Teams) {
                foreach(RTSUnitInstance unit in team.Units) {
                    CollisionController.CollideHeightmap(unit.CollisionGeometry, s.Map);
                    unit.GridPosition = unit.CollisionGeometry.Center;
                    unit.Height = unit.CollisionGeometry.Height;
                }
            }

        }

        // Cleanup Stage
        private void Cleanup(GameState s, float dt) {
            // Remove All Dead Entities
            foreach(var team in s.Teams) {
                team.RemoveAll(IsEntityDead);
                // TODO: Remove Empty Squads
                // team.RemoveAll(IsSquadEmpty);
            }

            // Add Newly Created Instances
            AddInstantiatedData(s);
        }
        private static bool IsEntityDead(IEntity e) {
            return !e.IsAlive;
        }
        private static bool IsSquadEmpty(ISquad s) {
            return s.EntityCount < 1;
        }
        public void AddInstantiatedData(GameState s) {

        }

        // Dev Callback
        public void OnDevCommand(string s) {
            DevCommand c;
            if(DevCommandSpawn.TryParse(s, out c)) {
                commands.Enqueue(c);
                return;
            }
        }
    }
}