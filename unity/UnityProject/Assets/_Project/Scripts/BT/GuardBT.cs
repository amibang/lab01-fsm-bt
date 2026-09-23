using UnityEngine;
using UnityEngine.AI;
using Lab01.AI;

namespace Lab01.BT
{
    public class GuardBT : MonoBehaviour
    {
        [SerializeField] private Perception perception;
        [SerializeField] private Transform[] waypoints;

        [Header("BT Settings")]
        [SerializeField] private float alertDuration = 1f;
        [SerializeField] private float attackDistance = 1.5f;
        [SerializeField] private float searchDuration = 10f;
        [SerializeField] private float loseTargetTimeout = 2f;
        [SerializeField] private float searchRadius = 3f;

        private NavMeshAgent _agent;
        private Node _root;

        private float _alertTimer;
        private float _lostTargetTimer;
        private float _searchTimer;

        private Vector3 _lastKnownPosition;
        private bool _hasLastKnownPosition;

        private int _wp;

        private enum Behavior
        {
            Patrol,
            Alert,
            Chase,
            Attack,
            Search
        }

        private Behavior _behavior = Behavior.Patrol;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            _root = new Selector(
                new Sequence(
                    new Condition(() => _behavior == Behavior.Alert),
                    new Action(Alert)
                ),

                new Sequence(
                    new Condition(() => _behavior == Behavior.Chase),
                    new Action(Chase)
                ),

                new Sequence(
                    new Condition(() => _behavior == Behavior.Attack),
                    new Action(Attack)
                ),

                new Sequence(
                    new Condition(() => _behavior == Behavior.Search),
                    new Action(Search)
                ),

                new Action(Patrol)
            );
        }

        private void Update()
        {
            if (_behavior == Behavior.Patrol && perception.CanSeePlayer)
            {
                SetBehavior(Behavior.Alert);
                _alertTimer = 0f;
                _agent.ResetPath();
            }

            _root.Tick();
        }

        private Status Alert()
        {
            if (!perception.CanSeePlayer)
            {
                SetBehavior(Behavior.Patrol);
                _alertTimer = 0f;
                return Status.Failure;
            }

            _lastKnownPosition = perception.Player.position;
            _hasLastKnownPosition = true;

            Vector3 direction = perception.Player.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            _alertTimer += Time.deltaTime;

            if (_alertTimer >= alertDuration)
            {
                _alertTimer = 0f;
                _lostTargetTimer = 0f;
                SetBehavior(Behavior.Chase);
                return Status.Success;
            }

            return Status.Running;
        }

        private void SetBehavior(Behavior next)
        {
            if (_behavior != next)
            {
                Debug.Log($"[BT] {_behavior} -> {next}");
                _behavior = next;
            }
        }

        private Status Chase()
        {
            if (!perception.CanSeePlayer)
            {
                _lostTargetTimer += Time.deltaTime;

                if (_lostTargetTimer >= loseTargetTimeout)
                {
                    _lostTargetTimer = 0f;
                    _searchTimer = 0f;
                    SetBehavior(Behavior.Search);
                    return Status.Success;
                }

                return Status.Running;
            }

            _lostTargetTimer = 0f;

            _lastKnownPosition = perception.Player.position;
            _hasLastKnownPosition = true;

            float distance = Vector3.Distance(
                transform.position,
                perception.Player.position
            );

            if (distance <= attackDistance)
            {
                SetBehavior(Behavior.Attack);
                return Status.Success;
            }

            _agent.SetDestination(_lastKnownPosition);

            return Status.Running;
        }

        private Status Attack()
        {
            if (!perception.CanSeePlayer)
            {
                _lostTargetTimer = 0f;
                SetBehavior(Behavior.Chase);
                return Status.Success;
            }

            float distance = Vector3.Distance(
                transform.position,
                perception.Player.position
            );

            if (distance > attackDistance)
            {
                SetBehavior(Behavior.Chase);
                return Status.Success;
            }

            _agent.ResetPath();

            Vector3 direction = perception.Player.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            return Status.Running;
        }

        private Status Search()
        {
            if (perception.CanSeePlayer)
            {
                _searchTimer = 0f;
                _lostTargetTimer = 0f;
                SetBehavior(Behavior.Chase);
                return Status.Success;
            }

            _searchTimer += Time.deltaTime;

            if (!_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                Vector3 randomOffset =
                    Random.insideUnitSphere * searchRadius;

                randomOffset.y = 0f;

                Vector3 searchPoint =
                    _lastKnownPosition + randomOffset;

                if (NavMesh.SamplePosition(
                    searchPoint,
                    out NavMeshHit hit,
                    searchRadius,
                    NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                }
            }

            if (_searchTimer >= searchDuration)
            {
                _searchTimer = 0f;
                _hasLastKnownPosition = false;
                SetBehavior(Behavior.Patrol);

                return Status.Success;
            }

            return Status.Running;
        }

        private Status Patrol()
        {
            if (perception.CanSeePlayer)
            {
                SetBehavior(Behavior.Alert);
                _alertTimer = 0f;
                _agent.ResetPath();
                return Status.Success;
            }

            if (waypoints == null || waypoints.Length == 0)
            {
                return Status.Running;
            }

            if (!_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                _agent.SetDestination(waypoints[_wp].position);
                _wp = (_wp + 1) % waypoints.Length;
            }

            return Status.Running;
        }

        private Status Investigate()
        {
            SetBehavior(Behavior.Search);
            return Search();
        }
    }
}