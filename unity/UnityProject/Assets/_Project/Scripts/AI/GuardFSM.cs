using UnityEngine;
using UnityEngine.AI;

namespace Lab01.AI
{
    public class GuardFSM : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Perception perception;

        [Header("Timers")]
        [SerializeField] private float alertTimeout = 1f;
        [SerializeField] private float loseTargetTimeout = 2f;
        [SerializeField] private float searchDuration = 10f;

        [Header("Combat")]
        [SerializeField] private float attackDistance = 1.5f;

        [Header("Search")]
        [SerializeField] private float searchRadius = 3f;

        public GuardState State { get; private set; } = GuardState.Patrol;

        private NavMeshAgent _agent;

        private float _alertTimer;
        private float _lostTargetTimer;
        private float _searchTimer;

        private int _wp;

        private Vector3 _lastKnownPosition;
        private bool _hasLastKnownPosition;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            switch (State)
            {
                case GuardState.Patrol:
                    Patrol();
                    break;

                case GuardState.Alert:
                    Alert();
                    break;

                case GuardState.Chase:
                    Chase();
                    break;

                case GuardState.Attack:
                    Attack();
                    break;

                case GuardState.Search:
                    Search();
                    break;

                case GuardState.Return:
                    ReturnToPost();
                    break;
            }
        }

        private void Transition(GuardState next)
        {
            Debug.Log($"[FSM] {State} -> {next}");
            State = next;
        }

        private void Patrol()
        {
            if (perception.CanSeePlayer)
            {
                _alertTimer = 0f;
                Transition(GuardState.Alert);
                return;
            }

            if (waypoints.Length == 0)
                return;

            if (!_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                _agent.SetDestination(
                    waypoints[_wp++ % waypoints.Length].position
                );
            }
        }

        private void Alert()
        {
            _alertTimer += Time.deltaTime;

            if (perception.CanSeePlayer)
            {
                _lastKnownPosition = perception.Player.position;
                _hasLastKnownPosition = true;
            }

            if (_alertTimer >= alertTimeout)
            {
                if (perception.CanSeePlayer)
                {
                    _lostTargetTimer = 0f;
                    Transition(GuardState.Chase);
                }
                else
                {
                    Transition(GuardState.Patrol);
                }
            }
        }

        private void Chase()
        {
            if (perception.CanSeePlayer)
            {
                _lastKnownPosition = perception.Player.position;
                _hasLastKnownPosition = true;
                _lostTargetTimer = 0f;

                _agent.SetDestination(_lastKnownPosition);

                float distance = Vector3.Distance(
                    transform.position,
                    perception.Player.position
                );

                if (distance <= attackDistance)
                {
                    Transition(GuardState.Attack);
                }

                return;
            }

            _lostTargetTimer += Time.deltaTime;

            if (_lostTargetTimer >= loseTargetTimeout)
            {
                _searchTimer = 0f;
                Transition(GuardState.Search);
            }
        }

        private void Attack()
        {
            if (!perception.CanSeePlayer)
            {
                _lostTargetTimer = 0f;
                Transition(GuardState.Chase);
                return;
            }

            float distance = Vector3.Distance(
                transform.position,
                perception.Player.position
            );

            if (distance > attackDistance)
            {
                Transition(GuardState.Chase);
                return;
            }

            _agent.ResetPath();

            Vector3 direction = perception.Player.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void Search()
        {
            if (perception.CanSeePlayer)
            {
                _lostTargetTimer = 0f;
                Transition(GuardState.Chase);
                return;
            }

            _searchTimer += Time.deltaTime;

            if (_hasLastKnownPosition)
            {
                if (!_agent.hasPath || _agent.remainingDistance < 0.5f)
                {
                    Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
                    randomOffset.y = 0f;

                    Vector3 searchPoint = _lastKnownPosition + randomOffset;

                    if (NavMesh.SamplePosition(
                        searchPoint,
                        out NavMeshHit hit,
                        searchRadius,
                        NavMesh.AllAreas))
                    {
                        _agent.SetDestination(hit.position);
                    }
                }
            }

            if (_searchTimer >= searchDuration)
            {
                _hasLastKnownPosition = false;
                Transition(GuardState.Patrol);
            }
        }

        private void ReturnToPost()
        {
            Transition(GuardState.Patrol);
        }
    }
}