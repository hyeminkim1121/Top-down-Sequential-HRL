using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class LowerAgentAblation : Agent
{
    public enum Team { Red = 0, Blue = 1 }
    public enum Position { Tank = 0, Artillery = 1, Infantry = 2 }

    [HideInInspector] public float MoveSpeed;
    [HideInInspector] public float ShootingRange;
    [HideInInspector] public float rewardWeight = 1f;
    [HideInInspector] public float attack;
    [HideInInspector] public float hpMax;
    [HideInInspector] public float hp;
    [HideInInspector] public float previousHp;

    [HideInInspector] public BehaviorParameters Behavior;
    [HideInInspector] public Rigidbody RigidBody;

    public NewEnvController EnvController;

    public Team team;
    public Position position;

    [SerializeField] LayerMask RaycastLayer;
    [SerializeField] private float rotationSpeed = 180f;

    [HideInInspector] public bool IsActive = true;
    [HideInInspector] public Agent CurrentTarget;

    public List<MARLAgent> EnemyLower = new();

    private List<GameObject> detectedEnemies = new();

    private int DecisionTime = 0;
    private int lastShotStep = -999;
    private int lastShotColorStep = -999;

    private float previousDistanceToTarget = -1f;

    // Episode reward logs
    [HideInInspector] public float Log_ApproachReward = 0f;
    [HideInInspector] public float Log_SurvivalReward = 0f;
    [HideInInspector] public float Log_DetectReward = 0f;
    [HideInInspector] public float Log_ShootReward = 0f;

    // ======================================================
    // Target Setter
    // ======================================================
    public void SetTarget(Agent target)
    {
        CurrentTarget = target;

        if (CurrentTarget != null)
        {
            previousDistanceToTarget =
                Vector3.Distance(transform.position, CurrentTarget.transform.position);
        }
    }

    // ======================================================
    // 초기화
    // ======================================================
    public override void Initialize()
    {
        RigidBody = GetComponent<Rigidbody>();
        Behavior = GetComponent<BehaviorParameters>();

        RigidBody.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        switch (position)
        {
            case Position.Tank:
                MoveSpeed = 6; ShootingRange = 60f; attack = 300f; hpMax = 1500f;
                break;
            case Position.Artillery:
                MoveSpeed = 3f; ShootingRange = 80f; attack = 300f; hpMax = 1000f;
                break;
            case Position.Infantry:
                MoveSpeed = 2f; ShootingRange = 50f; attack = 100f; hpMax = 500f;
                break;
        }

        previousHp = hpMax;
    }

    public override void OnEpisodeBegin()
    {
        hp = hpMax;
        previousHp = hpMax;

        previousDistanceToTarget = -1f;

        Log_ApproachReward = 0f;
        Log_SurvivalReward = 0f;
        Log_DetectReward = 0f;
        Log_ShootReward = 0f;
    }

    // ======================================================
    // 관측
    // ======================================================
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(hp / hpMax);
        sensor.AddObservation(transform.position.x / 300f);
        sensor.AddObservation(transform.position.z / 300f);
        sensor.AddObservation(transform.forward.x);
        sensor.AddObservation(transform.forward.z);
        sensor.AddObservation((int)position);

        if (CurrentTarget != null)
        {
            Vector3 relDir = (CurrentTarget.transform.position - transform.position).normalized;
            sensor.AddObservation(relDir.x);
            sensor.AddObservation(relDir.z);

            float dist = Vector3.Distance(transform.position, CurrentTarget.transform.position) / 300f;
            sensor.AddObservation(dist);

            float angle = Vector3.SignedAngle(transform.forward, relDir, Vector3.up) / 180f;
            sensor.AddObservation(angle);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    // ======================================================
    // 행동 처리
    // ======================================================
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        int action = actionBuffers.DiscreteActions[0];

        switch (action)
        {
            case 0: MoveForward(); break;
            case 1: MoveBackward(); break;
            case 2: MoveLeft(); break;
            case 3: MoveRight(); break;
            case 4: RotateTowardsTarget(); break;
            case 5: TryShoot(); break;
        }
        ApplyReward();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        if (CurrentTarget == null || !CurrentTarget.gameObject.activeSelf)
        {
            discreteActions[0] = 0;
            return;
        }

        Vector3 toTarget = CurrentTarget.transform.position - transform.position;
        float distance = toTarget.magnitude;
        float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);

        if (Mathf.Abs(angle) > 5f)
        {
            discreteActions[0] = 4;
            return;
        }

        if (distance < ShootingRange)
        {
            discreteActions[0] = 5;
            return;
        }

        discreteActions[0] = 0;
    }

    private void MoveForward() => RigidBody.velocity = transform.forward * MoveSpeed;
    private void MoveBackward() => RigidBody.velocity = -transform.forward * MoveSpeed;
    private void MoveLeft() => RigidBody.velocity = -transform.right * MoveSpeed;
    private void MoveRight() => RigidBody.velocity = transform.right * MoveSpeed;

    private void RotateTowardsTarget()
    {
        if (CurrentTarget == null) return;
        Vector3 dir = (CurrentTarget.transform.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, 360f * Time.deltaTime);
        }
    }

    // ======================================================
    // 보상
    // ======================================================
    private void ApplyReward()
    {
        if (CurrentTarget == null) return;

        float distance = Vector3.Distance(transform.position, CurrentTarget.transform.position);

        if (previousDistanceToTarget < 0f)
        {
            previousDistanceToTarget = distance;
            return;
        }

        float delta = previousDistanceToTarget - distance;
        float reward = (delta >= 0f) ? (delta * 2f) : (delta * 1f);

        EnvController.BlueGroup.AddGroupReward(reward);
        Log_ApproachReward += reward;

        previousDistanceToTarget = distance;
    }

    // ======================================================
    // 사격
    // ======================================================
    private void TryShoot()
    {
        if (CurrentTarget == null) return;
        if (DecisionTime - lastShotStep < 300) return;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, ShootingRange, RaycastLayer))
        {
            var target = hit.collider.GetComponent<MARLAgent>();
            if (target != null && target.IsActive)
            {
                EnvController.AgentShoot(this, target);
                lastShotStep = DecisionTime;
                lastShotColorStep = DecisionTime;

                Log_ShootReward += 1f;
            }
        }
    }

    // ======================================================
    // 레이센서 감지 보상
    // ======================================================
    private void RaySensorEnemyReward()
    {
        foreach (var obj in detectedEnemies)
        {
            if (obj == null) continue;

            EnvController.BlueGroup.AddGroupReward(0.001f);
            Log_DetectReward += 0.001f;
        }

        detectedEnemies.Clear();
    }

    public void OnDetection(GameObject detected)
    {
        if (detected.CompareTag("enemy"))
            detectedEnemies.Add(detected);
    }

    // ======================================================
    // FixedUpdate
    // ======================================================
    public void FixedUpdate()
    {
        Debug.DrawRay(transform.position, transform.forward * ShootingRange,
            DecisionTime - lastShotColorStep <= 50 ? Color.blue : Color.yellow);

        RaySensorEnemyReward();
        DecisionTime++;
    }
}
