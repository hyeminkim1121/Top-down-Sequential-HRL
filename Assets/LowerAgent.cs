using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

public class LowerAgent : Agent
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
    public Material DefaultMaterial;
    public Material DisabledMaterial;
    public NewEnvController EnvController;

    public Team team;
    public Position position;
    [SerializeField] LayerMask RaycastLayer;

    [HideInInspector] public bool IsActive = true;
    [HideInInspector] public Agent CurrentTarget;
    public List<MARLAgent> EnemyLower = new();

    private int DecisionTime = 0;
    private int lastShotStep = -999;
    private int lastShotColorStep = -999;
    private float lastShotTime = -999f;

    private float previousDistanceToTarget = -1f;

    // 보상 항목별 누적
    private float rewardTargetApproach = 0f;
    private float rewardSurvival = 0f;
    public float rewardGroupDamage = 0f;
    public float rewardDamageTaken = 0f;

    public override void Initialize()
    {
        RigidBody = GetComponent<Rigidbody>();
        Behavior = GetComponent<BehaviorParameters>();
        RigidBody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        switch (position)
        {
            case Position.Tank:
                MoveSpeed = 6f;
                ShootingRange = 60f;
                rewardWeight = 100f;
                attack = 300f;
                hpMax = 1500f;
                break;
            case Position.Artillery:
                MoveSpeed = 3f;
                ShootingRange = 80f;
                rewardWeight = 50f;
                attack = 300f;
                hpMax = 1000f;
                break;
            case Position.Infantry:
                MoveSpeed = 2f;
                ShootingRange = 50f;
                rewardWeight = 20f;
                attack = 100f;
                hpMax = 500f;
                break;
        }


    previousHp = hpMax;
    }

    public override void OnEpisodeBegin()
    {
        hp = hpMax;
        previousHp = hpMax;

        rewardTargetApproach = 0f;
        rewardSurvival = 0f;
        rewardGroupDamage = 0f;
        rewardDamageTaken = 0f;

        previousDistanceToTarget = -1f;
    }

    public void SetTarget(Agent target)
    {
        CurrentTarget = target;
        if (target != null)
        {
            previousDistanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        }
    }

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

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        int action = actionBuffers.DiscreteActions[0];

        Debug.Log($"{name} 액션: {action}");

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
        rewardTargetApproach += reward;

        previousDistanceToTarget = distance;
    }

    public void ReportEpisodeRewardToController()
    {
        float totalReward = rewardTargetApproach + rewardSurvival;
        EnvController.ReportLowerAgentReward(this, rewardTargetApproach, rewardSurvival, totalReward);
    }

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
            }
        }
    }

    public void FixedUpdate()
    {
        Debug.DrawRay(transform.position, transform.forward * ShootingRange,
            DecisionTime - lastShotColorStep <= 5 ? Color.blue : Color.yellow);

        DecisionTime++;
    }

    private void AvoidObstacle()
    {
        Vector3 avoidDirection = Vector3.Cross(transform.forward, Vector3.up).normalized;
        RigidBody.AddForce(avoidDirection * MoveSpeed, ForceMode.VelocityChange);
        Debug.Log($"{name} is avoiding an obstacle.");
    }
}
