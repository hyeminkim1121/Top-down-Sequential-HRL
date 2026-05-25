using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MARLAgent : Agent
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
    [HideInInspector] public Rigidbody Rb;

    public NewEnvController EnvController;

    public Team team;
    public Position position;

    [SerializeField] LayerMask AgentLayer;
    [SerializeField] private float rotationSpeed = 180f;

    [HideInInspector] public bool IsActive = true;
    [HideInInspector] public Agent CurrentTarget;

    public List<LowerAgent> EnemyLower = new();

    private List<GameObject> detectedEnemies = new();

    public int MaxObservedEnemies = 6;

    private int DecisionTime = 0;
    private int lastShotStep = -999;
    private int lastShotColorStep = -999;

    private float previousDistanceToTarget = -1f;

    // Episode reward logs
    [HideInInspector] public float Log_ApproachReward = 0f;
    [HideInInspector] public float Log_ShootReward = 0f;
    [HideInInspector] public float Log_DetectReward = 0f;

    // ======================================================
    // Target Setter (LowerAgent와 동일)
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
    // 초기화 (LowerAgent와 동일한 스탯)
    // ======================================================
    public override void Initialize()
    {
        Rb = GetComponent<Rigidbody>();
        Behavior = GetComponent<BehaviorParameters>();

        Rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        switch (position)
        {
            case Position.Tank:
                MoveSpeed = 6f; ShootingRange = 60f; attack = 300f; hpMax = 1500f;
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
        Log_ShootReward = 0f;
        Log_DetectReward = 0f;
    }

    // ======================================================
    // 관측: 자기 정보 + 타겟 정보 + 환경 제공 적군 정보
    // ======================================================
    // Self: hp, x, z, forward_x, forward_z, position = 6
    // Current Target: rel_dir_x, rel_dir_z, dist, angle = 4
    // Enemy Info (환경 제공, 거리순 정렬):
    //   per enemy: rel_x, rel_z, dist_norm, hp_ratio, attack, isInf, isArt, isTank = 8
    //   total: MaxObservedEnemies * 8
    // -----------------------------------------------
    // Total = 6 + 4 + MaxObservedEnemies * 8
    // ======================================================
    public override void CollectObservations(VectorSensor sensor)
    {
        // --- 자기 정보 (LowerAgent와 동일) ---
        sensor.AddObservation(hp / hpMax);
        sensor.AddObservation(transform.position.x / 300f);
        sensor.AddObservation(transform.position.z / 300f);
        sensor.AddObservation(transform.forward.x);
        sensor.AddObservation(transform.forward.z);
        sensor.AddObservation((int)position);

        // --- 현재 타겟 정보 (LowerAgent와 동일) ---
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

        // --- 환경 제공 적군 정보 (거리순 정렬, 패딩) ---
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);
        aliveEnemies.Sort((a, b) =>
        {
            float da = (a.transform.position - transform.position).sqrMagnitude;
            float db = (b.transform.position - transform.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        int k = Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);

        for (int i = 0; i < MaxObservedEnemies; i++)
        {
            if (i < k)
            {
                var enemy = aliveEnemies[i];
                Vector3 rel = enemy.transform.position - transform.position;
                float d = rel.magnitude;

                sensor.AddObservation(rel.x / 300f);
                sensor.AddObservation(rel.z / 300f);
                sensor.AddObservation(Mathf.Clamp01(d / 600f));
                sensor.AddObservation(enemy.hp / enemy.hpMax);
                sensor.AddObservation(enemy.attack * 0.01f);

                float isInf = enemy.position == LowerAgent.Position.Infantry ? 1f : 0f;
                float isArt = enemy.position == LowerAgent.Position.Artillery ? 1f : 0f;
                float isTank = enemy.position == LowerAgent.Position.Tank ? 1f : 0f;
                sensor.AddObservation(isInf);
                sensor.AddObservation(isArt);
                sensor.AddObservation(isTank);
            }
            else
            {
                for (int j = 0; j < 8; j++) sensor.AddObservation(0f);
            }
        }
    }

    // ======================================================
    // 행동: Branch 0 = 이동(6), Branch 1 = 타겟 선택(MaxObservedEnemies)
    // ======================================================
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var da = actionBuffers.DiscreteActions;

        // Branch 0: 이동 (LowerAgent와 동일한 6가지 행동)
        int moveAction = da[0];
        switch (moveAction)
        {
            case 0: MoveForward(); break;
            case 1: MoveBackward(); break;
            case 2: MoveLeft(); break;
            case 3: MoveRight(); break;
            case 4: RotateTowardsTarget(); break;
            case 5: TryShoot(); break;
        }

        // Branch 1: 타겟 선택 (flat MARL이 직접 학습)
        int targetAction = da[1];
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);
        if (aliveEnemies.Count > 0)
        {
            aliveEnemies.Sort((a, b) =>
            {
                float distA = (a.transform.position - transform.position).sqrMagnitude;
                float distB = (b.transform.position - transform.position).sqrMagnitude;
                return distA.CompareTo(distB);
            });

            int idx = targetAction % Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);
            SetTarget(aliveEnemies[idx]);
        }

        ApplyReward();
    }

    // ======================================================
    // 이동 (LowerAgent와 동일)
    // ======================================================
    private void MoveForward() => Rb.velocity = transform.forward * MoveSpeed;
    private void MoveBackward() => Rb.velocity = -transform.forward * MoveSpeed;
    private void MoveLeft() => Rb.velocity = -transform.right * MoveSpeed;
    private void MoveRight() => Rb.velocity = transform.right * MoveSpeed;

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
    // 보상 (LowerAgent와 동일 구조, RedGroup 사용)
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

        EnvController.RedGroup.AddGroupReward(reward);
        Log_ApproachReward += reward;

        previousDistanceToTarget = distance;
    }

    // ======================================================
    // 사격 (LowerAgent와 동일한 쿨다운 300, 단일 Raycast)
    // ======================================================
    private void TryShoot()
    {
        if (CurrentTarget == null) return;
        if (DecisionTime - lastShotStep < 300) return;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, ShootingRange, AgentLayer))
        {
            var target = hit.collider.GetComponent<LowerAgent>();
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
    // 레이센서 감지 보상 (LowerAgent와 동일 구조)
    // ======================================================
    private void RaySensorEnemyReward()
    {
        foreach (var obj in detectedEnemies)
        {
            if (obj == null) continue;

            EnvController.RedGroup.AddGroupReward(0.001f);
            Log_DetectReward += 0.001f;
        }

        detectedEnemies.Clear();
    }

    public void OnDetection(GameObject detected)
    {
        if (detected.CompareTag("blue"))
            detectedEnemies.Add(detected);
    }

    // ======================================================
    // FixedUpdate
    // ======================================================
    public void FixedUpdate()
    {
        if (!IsActive) return;

        Debug.DrawRay(transform.position, transform.forward * ShootingRange,
            DecisionTime - lastShotColorStep <= 50 ? Color.red : Color.yellow);

        RaySensorEnemyReward();
        DecisionTime++;
    }
}
