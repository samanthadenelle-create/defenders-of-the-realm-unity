// GuardPatrol.cs — simple NavMeshAgent waypoint-patrol AI for enemy-outpost guards.
// Reproduced for the sandbox outpost test. Walks the guard between its assigned waypoints
// in order, pausing waitTimeAtPoint seconds at each. All agent access is navmesh-guarded
// (agent.isOnNavMesh) so a guard spawned BEFORE the navmesh is baked simply no-ops rather
// than throwing — patrol begins once the agent is on a baked NavMesh at Play.

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace DeNelle.Sandbox
{
    public class GuardPatrol : MonoBehaviour
    {
        public List<Transform> waypoints = new List<Transform>();
        public float waitTimeAtPoint = 2f;
        public float speed = 3.5f;

        NavMeshAgent agent;
        int currentWaypoint = 0;
        float waitTimer = 0f;
        bool isWaiting = false;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = speed;
                GotoNextWaypoint();
            }
        }

        void Update()
        {
            if (agent == null || waypoints.Count == 0) return;

            if (isWaiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0)
                {
                    isWaiting = false;
                    GotoNextWaypoint();
                }
                return;
            }

            if (!agent.pathPending && agent.remainingDistance < 0.8f)
            {
                isWaiting = true;
                waitTimer = waitTimeAtPoint;
            }
        }

        void GotoNextWaypoint()
        {
            if (waypoints.Count == 0) return;
            currentWaypoint = (currentWaypoint + 1) % waypoints.Count;
            if (agent.isOnNavMesh) agent.SetDestination(waypoints[currentWaypoint].position);
        }

        public void SetPatrolPoints(List<Transform> points)
        {
            waypoints = new List<Transform>(points);
            if (agent != null && waypoints.Count > 0 && agent.isOnNavMesh)
                agent.SetDestination(waypoints[0].position);
        }
    }
}
