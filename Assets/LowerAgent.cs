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

    private float previousDistanceToTarget;


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
                MoveSpeed = 4f;
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

        if (CurrentTarget != null)
        {
            previousDistanceToTarget = Vector3.Distance(transform.position, CurrentTarget.transform.position);

        }
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
        if (CurrentTarget == null)
            sensor.AddObservation(Vector3.zero);
        else
            foreach (var enemy in EnemyLower)
            {
                sensor.AddObservation(enemy.IsActive ? 1f : 0f);
                sensor.AddObservation(enemy.transform.position);
                sensor.AddObservation(enemy.hpMax);
                sensor.AddObservation(enemy.MoveSpeed);
                sensor.AddObservation(enemy.rewardWeight);
                sensor.AddObservation(enemy.ShootingRange);
            }

        sensor.AddObservation(IsActive ? 1f : 0f);
        sensor.AddObservation(transform.position);
        sensor.AddObservation((int)position);
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
        float distanceDelta = previousDistanceToTarget - distance;
        float rewardA = distanceDelta * 4f; // 거리 가중치 4 (TD 기준)
        EnvController.BlueGroup.AddGroupReward(rewardA);
        rewardTargetApproach += rewardA;
        Debug.Log($"{name} TargetApproach Reward: {rewardA:F3}");



        float rewardB = 0.02f; // 스텝당 생존 보상 (고정값)
        EnvController.BlueGroup.AddGroupReward(rewardB);
        rewardSurvival += rewardB;
        Debug.Log($"{name} Survival Reward: {rewardB:F3}");

        previousDistanceToTarget = distance;
        previousHp = hp;
    }

    public void ReportEpisodeRewardToController()
    {
        float totalReward = rewardTargetApproach + rewardSurvival;
        EnvController.ReportLowerAgentReward(this, rewardTargetApproach, rewardSurvival, totalReward);
    }

    private void TryShoot()
    {
        if (CurrentTarget == null || DecisionTime - lastShotStep < 15) return;

        RaycastHit hit;
        bool hitSuccess = false;

        if (position == Position.Tank || position == Position.Infantry)
        {
            if (Physics.Raycast(transform.position, transform.forward, out hit, ShootingRange, RaycastLayer))
            {
                // MARLAgent (RSA 상대) 또는 LowerAgent (교전 상대) 둘 다 탐지
                Agent target = hit.collider.GetComponent<MARLAgent>() as Agent
                            ?? hit.collider.GetComponent<LowerAgent>() as Agent;

                if (target != null && target != this && IsEnemy(target))
                {
                    EnvController.AgentShoot(this, target);
                    hitSuccess = true;
                }
            }
        }
        else if (position == Position.Artillery)
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, ShootingRange, RaycastLayer);
            foreach (var eachHit in hits)
            {
                Agent target = eachHit.collider.GetComponent<MARLAgent>() as Agent
                            ?? eachHit.collider.GetComponent<LowerAgent>() as Agent;

                if (target != null && target != this && IsEnemy(target))
                {
                    EnvController.AgentShoot(this, target);
                    hitSuccess = true;
                    break;
                }
            }
        }

        if (hitSuccess)
        {
            lastShotStep = DecisionTime;
            lastShotColorStep = DecisionTime;
            lastShotTime = Time.fixedTime;
        }
    }

    private bool IsEnemy(Agent target)
    {
        // 자기가 Blue(LowerAgent)면 적은 MARLAgent 또는 RedLowerAgent
        // 자기가 Red(RedLowerAgent)면 적은 Blue LowerAgent
        bool iAmRed = this is RedLowerAgent;
        bool targetIsRed = target is RedLowerAgent;
        bool targetIsMarl = target is MARLAgent;

        if (iAmRed)
            return !targetIsRed && target is LowerAgent; // Red → Blue만 공격
        else
            return targetIsRed || targetIsMarl; // Blue → Red 또는 MARLAgent 공격
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
