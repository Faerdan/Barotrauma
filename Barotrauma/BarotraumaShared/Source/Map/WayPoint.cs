﻿using Barotrauma.Items.Components;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
//using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    public enum SpawnType { Path, Human, Enemy, Cargo };
    partial class WayPoint : MapEntity
    {
        public static List<WayPoint> WayPointList = new List<WayPoint>();

        public static bool ShowWayPoints = true, ShowSpawnPoints = true;

        protected SpawnType spawnType;

        //characters spawning at the waypoint will be given an ID card with these tags
        private string[] idCardTags;

        //only characters with this job will be spawned at the waypoint
        private JobPrefab assignedJob;

        private Hull currentHull;

        private ushort ladderId;
        public Ladder Ladders;

        private ushort gapId;
        public Gap ConnectedGap
        {
            get;
            private set;
        }

        public Hull CurrentHull
        {
            get { return currentHull; }
        }

        public SpawnType SpawnType
        {
            get { return spawnType; }
            set { spawnType = value; }
        }

        public override string Name
        {
            get
            {
                return spawnType == SpawnType.Path ? "WayPoint" : "SpawnPoint";
            }
        }

        public string[] IdCardTags
        {
            get { return idCardTags; }
            private set
            {
                idCardTags = value;
                for (int i = 0; i<idCardTags.Length; i++)
                {
                    idCardTags[i] = idCardTags[i].Trim();
                }
            }
        }

        public WayPoint(Vector2 position, SpawnType spawnType, Submarine submarine, Gap gap = null)
            : this(new Rectangle((int)position.X-3, (int)position.Y+3, 6, 6), submarine)
        {
            this.spawnType = spawnType;
            ConnectedGap = gap;
        }

        public WayPoint(MapEntityPrefab prefab, Rectangle rectangle)
           : this (rectangle, Submarine.MainSub)
        { 
            if (prefab.Name.Contains("Spawn"))
            {
                spawnType = SpawnType.Human;
            }
            else
            {
                SpawnType = SpawnType.Path;
            }
        }

        public WayPoint(Rectangle newRect, Submarine submarine)
            : base (null, submarine)
        {
            rect = newRect;
            linkedTo = new ObservableCollection<MapEntity>();
            idCardTags = new string[0];

#if CLIENT
            if (iconTexture==null)
            {
                iconTexture = Sprite.LoadTexture("Content/Map/waypointIcons.png");
            }
#endif

            InsertToList();
            WayPointList.Add(this);

            currentHull = Hull.FindHull(WorldPosition);
        }

        public override MapEntity Clone()
        {
            var clone = new WayPoint(rect, Submarine);
            clone.idCardTags = idCardTags;
            clone.spawnType = spawnType;
            clone.assignedJob = assignedJob;

            return clone;
        }

        public override bool IsMouseOn(Vector2 position)
        {
#if CLIENT
            if (IsHidden()) return false;
#endif

            return base.IsMouseOn(position);
        }

        public static void GenerateSubWaypoints(Submarine submarine)
        {
            if (!Hull.hullList.Any())
            {
                DebugConsole.ThrowError("Couldn't generate waypoints: no hulls found.");
                return;
            }

            List<WayPoint> existingWaypoints = WayPointList.FindAll(wp => wp.spawnType == SpawnType.Path);
            foreach (WayPoint wayPoint in existingWaypoints)
            {
                wayPoint.Remove();
            }

            float minDist = 150.0f;
            float heightFromFloor = 110.0f;

            foreach (Hull hull in Hull.hullList)
            {
                if (hull.Rect.Height < 150) continue;

                WayPoint prevWaypoint = null;

                if (hull.Rect.Width<minDist*3.0f)
                {
                    new WayPoint(
                        new Vector2(hull.Rect.X + hull.Rect.Width / 2.0f, hull.Rect.Y - hull.Rect.Height + heightFromFloor), SpawnType.Path, submarine);
                    continue;
                }

                for (float x = hull.Rect.X + minDist; x <= hull.Rect.Right - minDist; x += minDist)
                {
                    var wayPoint = new WayPoint(new Vector2(x, hull.Rect.Y - hull.Rect.Height + heightFromFloor), SpawnType.Path, submarine);

                    if (prevWaypoint != null) wayPoint.ConnectTo(prevWaypoint);                    

                    prevWaypoint = wayPoint;
                }
            }
            
            float outSideWaypointInterval = 200.0f;
            int outsideWaypointDist = 100;

            Rectangle borders = Hull.GetBorders();

            borders.X -= outsideWaypointDist;
            borders.Y += outsideWaypointDist;

            borders.Width += outsideWaypointDist * 2;
            borders.Height += outsideWaypointDist * 2;

            borders.Location -= MathUtils.ToPoint(submarine.HiddenSubPosition);
                
            if (borders.Width <= outSideWaypointInterval*2)
            {
                borders.Inflate(outSideWaypointInterval*2 - borders.Width, 0);
            }

            if (borders.Height <= outSideWaypointInterval * 2)
            {
                int inflateAmount = (int)(outSideWaypointInterval * 2) - borders.Height;
                borders.Y += inflateAmount / 2;

                borders.Height += inflateAmount;
            }
            
            WayPoint[,] cornerWaypoint = new WayPoint[2,2];

            for (int i = 0; i<2; i++)
            {
                for (float x = borders.X + outSideWaypointInterval; x < borders.Right - outSideWaypointInterval; x += outSideWaypointInterval)
                {
                    var wayPoint = new WayPoint(
                        new Vector2(x, borders.Y - borders.Height * i) + submarine.HiddenSubPosition, 
                        SpawnType.Path, submarine);

                    if (x == borders.X + outSideWaypointInterval)
                    {
                        cornerWaypoint[i, 0] = wayPoint;
                    }
                    else
                    {
                        wayPoint.ConnectTo(WayPoint.WayPointList[WayPointList.Count-2]);
                    }
                }

                cornerWaypoint[i, 1] = WayPoint.WayPointList[WayPointList.Count - 1];
            }
             
            for (int i = 0; i < 2; i++)
            {
                WayPoint wayPoint = null;
                for (float y = borders.Y - borders.Height; y < borders.Y; y += outSideWaypointInterval)
                {
                    wayPoint = new WayPoint(
                        new Vector2(borders.X + borders.Width * i, y) + submarine.HiddenSubPosition, 
                        SpawnType.Path, submarine);

                    if (y == borders.Y - borders.Height)
                    {
                        wayPoint.ConnectTo(cornerWaypoint[1, i]);
                    }
                    else
                    {
                        wayPoint.ConnectTo(WayPoint.WayPointList[WayPointList.Count - 2]);
                    }
                }

                wayPoint.ConnectTo(cornerWaypoint[0, i]);
            }

            List<Structure> stairList = new List<Structure>();
            foreach (MapEntity me in mapEntityList)
            {
                Structure stairs = me as Structure;
                if (stairs == null) continue;

                if (stairs.StairDirection != Direction.None) stairList.Add(stairs);
            }

            foreach (Structure stairs in stairList)
            {
                WayPoint[] stairPoints = new WayPoint[3];

                stairPoints[0] = new WayPoint(
                    new Vector2(stairs.Rect.X - 32.0f,
                        stairs.Rect.Y - (stairs.StairDirection == Direction.Left ? 80 : stairs.Rect.Height) + heightFromFloor), SpawnType.Path, submarine);

                stairPoints[1] = new WayPoint(
                  new Vector2(stairs.Rect.Right + 32.0f,
                      stairs.Rect.Y - (stairs.StairDirection == Direction.Left ? stairs.Rect.Height : 80) + heightFromFloor), SpawnType.Path, submarine);

                for (int i = 0; i < 2; i++ )
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        WayPoint closest = stairPoints[i].FindClosest(dir, true, new Vector2(-30.0f,30f));
                        if (closest == null) continue;
                        stairPoints[i].ConnectTo(closest);
                    }                    
                }
                
                stairPoints[2] = new WayPoint((stairPoints[0].Position + stairPoints[1].Position)/2, SpawnType.Path, submarine);
                stairPoints[0].ConnectTo(stairPoints[2]);
                stairPoints[2].ConnectTo(stairPoints[1]);
            }

            foreach (Item item in Item.ItemList)
            {
                var ladders = item.GetComponent<Items.Components.Ladder>();
                if (ladders == null) continue;

                WayPoint[] ladderPoints = new WayPoint[2];

                ladderPoints[0] = new WayPoint(new Vector2(item.Rect.Center.X, item.Rect.Y - item.Rect.Height + heightFromFloor), SpawnType.Path, submarine);
                ladderPoints[1] = new WayPoint(new Vector2(item.Rect.Center.X, item.Rect.Y-1.0f), SpawnType.Path, submarine);                

                WayPoint prevPoint = ladderPoints[0];
                Vector2 prevPos = prevPoint.SimPosition;

                List<Body> ignoredBodies = new List<Body>();

                while (prevPoint != ladderPoints[1])
                {
                    var pickedBody = Submarine.PickBody(prevPos, ladderPoints[1].SimPosition, ignoredBodies);

                    if (pickedBody == null) break;

                    ignoredBodies.Add(pickedBody);

                    if (pickedBody.UserData is Item)
                    {
                        var door = ((Item)pickedBody.UserData).GetComponent<Door>();
                        if (door != null)
                        {
                            WayPoint newPoint = new WayPoint(door.Item.Position, SpawnType.Path, submarine);
                            newPoint.Ladders = ladders;
                            newPoint.ConnectedGap = door.LinkedGap;

                            newPoint.ConnectTo(prevPoint);

                            prevPoint = newPoint;

                            prevPos = ConvertUnits.ToSimUnits(door.Item.Position - Vector2.UnitY * door.Item.Rect.Height);
                        }
                        else
                        {
                            prevPos = Submarine.LastPickedPosition;
                        }
                    }
                    else
                    {
                        prevPos = Submarine.LastPickedPosition;
                    }
                }

                prevPoint.ConnectTo(ladderPoints[1]);



                //for (float y = ladderPoints[0].Position.Y+100.0f; y < ladderPoints[1].Position.Y; y+=100.0f )
                //{
                //    var midPoint = new WayPoint(new Vector2(item.Rect.Center.X, y), SpawnType.Path, Submarine.Loaded);
                //    midPoint.Ladders = ladders;

                //    midPoint.ConnectTo(prevPoint);
                //    prevPoint = midPoint;
                //}
                //ladderPoints[1].ConnectTo(prevPoint);

                for (int i = 0; i < 2; i++)
                {
                    ladderPoints[i].Ladders = ladders;

                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        WayPoint closest = ladderPoints[i].FindClosest(dir, true, new Vector2(-150.0f, 10f));
                        if (closest == null) continue;
                        ladderPoints[i].ConnectTo(closest);
                    }
                }

                //ladderPoints[0].ConnectTo(ladderPoints[1]);
            }
                        
            foreach (Gap gap in Gap.GapList)
            {
                if (!gap.isHorizontal) continue;
                
                //too small to walk through
                if (gap.Rect.Height < 150.0f) continue;

                var wayPoint = new WayPoint(
                    new Vector2(gap.Rect.Center.X, gap.Rect.Y - gap.Rect.Height + heightFromFloor), SpawnType.Path, submarine, gap);

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float tolerance = gap.IsRoomToRoom ? 50.0f : outSideWaypointInterval / 2.0f;

                    WayPoint closest = wayPoint.FindClosest(
                        dir, true, new Vector2(-tolerance, tolerance), 
                        gap.ConnectedDoor == null ? null : gap.ConnectedDoor.Body.FarseerBody);

                    if (closest != null)
                    {
                        wayPoint.ConnectTo(closest);
                    }
                }
            }

            foreach (Gap gap in Gap.GapList)
            {
                if (gap.isHorizontal || gap.IsRoomToRoom) continue;

                //too small to walk through
                if (gap.Rect.Width < 100.0f) continue;

                var wayPoint = new WayPoint(
                    new Vector2(gap.Rect.Center.X, gap.Rect.Y - gap.Rect.Height/2), SpawnType.Path, submarine, gap);

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    WayPoint closest = wayPoint.FindClosest(dir, false, new Vector2(-outSideWaypointInterval, outSideWaypointInterval) / 2.0f);
                    if (closest == null) continue;
                    wayPoint.ConnectTo(closest);
                }
            }

            var orphans = WayPointList.FindAll(w => w.spawnType == SpawnType.Path && !w.linkedTo.Any());

            foreach (WayPoint wp in orphans)
            {
                wp.Remove();
            }
        }

        private WayPoint FindClosest(int dir, bool horizontalSearch, Vector2 tolerance, Body ignoredBody = null)
        {
            if (dir != -1 && dir != 1) return null;

            float closestDist = 0.0f;
            WayPoint closest = null;


            foreach (WayPoint wp in WayPointList)
            {
                if (wp.SpawnType != SpawnType.Path || wp == this) continue;

                float diff = 0.0f;
                if (horizontalSearch)
                {
                    if ((wp.Position.Y - Position.Y) < tolerance.X || (wp.Position.Y - Position.Y) > tolerance.Y) continue;

                    diff = wp.Position.X - Position.X;
                }
                else
                {
                    if ((wp.Position.X - Position.X) < tolerance.X || (wp.Position.X - Position.X) > tolerance.Y) continue;

                    diff = wp.Position.Y - Position.Y;
                }

                if (Math.Sign(diff) != dir) continue;

                float dist = Vector2.Distance(wp.Position, Position);
                if (closest == null || dist < closestDist)
                {
                    var body = Submarine.CheckVisibility(SimPosition, wp.SimPosition, true, true);
                    if (body != null && body != ignoredBody && !(body.UserData is Submarine))
                    {
                        if (body.UserData is Structure || body.FixtureList[0].CollisionCategories.HasFlag(Physics.CollisionWall)) continue;
                    }
                    
                    closestDist = dist;
                    closest = wp;
                }
            }
            

            return closest;
        }

        private void ConnectTo(WayPoint wayPoint2)
        {
            System.Diagnostics.Debug.Assert(this != wayPoint2);

            if (!linkedTo.Contains(wayPoint2)) linkedTo.Add(wayPoint2);
            if (!wayPoint2.linkedTo.Contains(this)) wayPoint2.linkedTo.Add(this);
        }

        public static WayPoint GetRandom(SpawnType spawnType = SpawnType.Human, Job assignedJob = null, Submarine sub = null, bool useSyncedRand = false)
        {
            List<WayPoint> wayPoints = new List<WayPoint>();

            foreach (WayPoint wp in WayPointList)
            {
                if (sub != null && wp.Submarine != sub) continue;
                if (wp.spawnType != spawnType) continue;
                if (assignedJob != null && wp.assignedJob != assignedJob.Prefab) continue;

                wayPoints.Add(wp);
            }

            if (!wayPoints.Any()) return null;

            return wayPoints[Rand.Int(wayPoints.Count, (useSyncedRand ? Rand.RandSync.Server : Rand.RandSync.Unsynced))];
        }

        public static WayPoint[] SelectCrewSpawnPoints(List<CharacterInfo> crew, Submarine submarine, bool tryAssignWayPoint = true)
        {
            List<WayPoint> subWayPoints = WayPointList.FindAll(wp => wp.Submarine == submarine);

            List<WayPoint> unassignedWayPoints = subWayPoints.FindAll(wp => wp.spawnType == SpawnType.Human);

            WayPoint[] assignedWayPoints = new WayPoint[crew.Count];

            for (int i = 0; i < crew.Count; i++ )
            {
                //try to give the crew member a spawnpoint that hasn't been assigned to anyone and matches their job                
                for (int n = 0; n < unassignedWayPoints.Count; n++)
                {
                    if (crew[i].Job.Prefab != unassignedWayPoints[n].assignedJob) continue;
                    assignedWayPoints[i] = unassignedWayPoints[n];
                    unassignedWayPoints.RemoveAt(n);

                    break;
                }                
            }

            //go through the crewmembers that don't have a spawnpoint yet (if any)
            for (int i = 0; i < crew.Count; i++)
            {
                if (assignedWayPoints[i] != null) continue;

                //try to assign a spawnpoint that matches the job, even if the spawnpoint is already assigned to someone else
                foreach (WayPoint wp in subWayPoints)
                {
                    if (wp.spawnType != SpawnType.Human || wp.assignedJob != crew[i].Job.Prefab) continue;

                    assignedWayPoints[i] = wp;
                    break;
                }

                if (assignedWayPoints[i] != null) continue;

                if (tryAssignWayPoint)
                {
                    //try to assign a spawnpoint that isn't meant for any specific job
                    var nonJobSpecificPoints = subWayPoints.FindAll(wp => wp.spawnType == SpawnType.Human && wp.assignedJob == null);

                    if (nonJobSpecificPoints.Any())
                    {
                        assignedWayPoints[i] = nonJobSpecificPoints[Rand.Int(nonJobSpecificPoints.Count, Rand.RandSync.Server)];
                    }
                }

                if (assignedWayPoints[i] != null) continue;

                //everything else failed -> just give a random spawnpoint
                assignedWayPoints[i] = GetRandom(SpawnType.Human);
            }

            for (int i = 0; i < assignedWayPoints.Length; i++ )
            {
                if (assignedWayPoints[i]==null)
                {
                    DebugConsole.ThrowError("Couldn't find a waypoint for " + crew[i].Name + "!");
                    assignedWayPoints[i] = WayPointList[0];
                }
            }

            return assignedWayPoints;
        }

        public override void OnMapLoaded()
        {
            currentHull = Hull.FindHull(WorldPosition, currentHull);

            if (gapId > 0) ConnectedGap = FindEntityByID(gapId) as Gap;

            if (ladderId > 0)
            {
                var ladderItem = FindEntityByID(ladderId) as Item;

                if (ladderItem != null) Ladders = ladderItem.GetComponent<Ladder>();
            }
        }

        public static void Load(XElement element, Submarine submarine)
        {
            Rectangle rect = new Rectangle(
                int.Parse(element.Attribute("x").Value),
                int.Parse(element.Attribute("y").Value),
                (int)Submarine.GridSize.X, (int)Submarine.GridSize.Y);

            WayPoint w = new WayPoint(rect, submarine);

            w.ID = (ushort)int.Parse(element.Attribute("ID").Value);

            Enum.TryParse<SpawnType>(ToolBox.GetAttributeString(element, "spawn", "Path"), out w.spawnType);

            string idCardTagString = ToolBox.GetAttributeString(element, "idcardtags", "");
            if (!string.IsNullOrWhiteSpace(idCardTagString))
            {
                w.IdCardTags = idCardTagString.Split(',');
            }

            string jobName = ToolBox.GetAttributeString(element, "job", "").ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(jobName))
            {
                w.assignedJob = JobPrefab.List.Find(jp => jp.Name.ToLowerInvariant() == jobName);
            }

            w.ladderId = (ushort)ToolBox.GetAttributeInt(element, "ladders", 0);
            w.gapId = (ushort)ToolBox.GetAttributeInt(element, "gap", 0);

            w.linkedToID = new List<ushort>();
            int i = 0;
            while (element.Attribute("linkedto" + i) != null)
            {
                w.linkedToID.Add((ushort)int.Parse(element.Attribute("linkedto" + i).Value));
                i += 1;
            }
        }

        public override void ShallowRemove()
        {
            base.ShallowRemove();

            WayPointList.Remove(this);
        }

        public override void Remove()
        {
            base.Remove();

            WayPointList.Remove(this);
        }
    
    }
}
