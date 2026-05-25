using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Red팀 교전용 UpperAgent (BU/CC 모델 호환).
/// BU Stage 2의 UpperAgentAblation과 동일한 obs 구조 (Space Size 90).
/// 적을 LowerAgent 타입으로 받음.
/// Inspector에서 Continuous Actions = 3, Use Child Sensors ✅ 설정 필요.
/// 하위 RedLowerAgent에 Ray Perception Sensor 3D 필요.
/// </summary>
public class RedBattleUpperAgent : Agent
{
    public NewEnvController EnvController;

    [Tooltip("우리팀 RedLowerAgents")]
    public List<LowerAgent> MyLower = new();

    [Tooltip("적군 Blue LowerAgents")]
    public List<LowerAgent> EnemyLower = new();

    [Header("Observation Settings")]
    public int MaxObservedEnemies = 6;
    public float PositionNorm = 600f;
    public float StatScale = 0.01f;

    public int TargetAssignInterval = 20;
    private int assignStepCounter = 0;
    private int stepsSinceDecision = 0;

    public void RegisterAgents(LowerAgent lower, LowerAgent enemy)
    {
        if (lower != null && !MyLower.Contains(lower))
            MyLower.Add(lower);
        if (enemy != null && !EnemyLower.Contains(enemy))
            EnemyLower.Add(enemy);
    }

    public override void OnEpisodeBegin()
    {
        stepsSinceDecision = 0;
        assignStepCounter = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var aliveAllies = MyLower.FindAll(a => a != null && a.IsActive);
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);

        sensor.AddObservation(aliveAllies.Count / 10f);
        sensor.AddObservation(aliveEnemies.Count / 10f);

        Vector3 teamCenter = ComputeCenter(aliveAllies);
        Vector3 enemyCenter = ComputeCenter(aliveEnemies);

        sensor.AddObservation(teamCenter.x / PositionNorm);
        sensor.AddObservation(teamCenter.z / PositionNorm);

        Vector3 relEnemyCenter = enemyCenter - teamCenter;
        sensor.AddObservation(relEnemyCenter.x / PositionNorm);
        sensor.AddObservation(relEnemyCenter.z / PositionNorm);

        float totalEnemyHp = 0f;
        float totalEnemyAttack = 0f;
        foreach (var e in aliveEnemies)
        {
            totalEnemyHp += e.hp;
            totalEnemyAttack += e.attack;
        }
        sensor.AddObservation(totalEnemyHp * StatScale);
        sensor.AddObservation(totalEnemyAttack * StatScale);

        aliveEnemies.Sort((a, b) =>
        {
            float da = (a.transform.position - teamCenter).sqrMagnitude;
            float db = (b.transform.position - teamCenter).sqrMagnitude;
            return da.CompareTo(db);
        });

        int k = Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);

        for (int i = 0; i < MaxObservedEnemies; i++)
        {
            if (i < k)
            {
                var enemy = aliveEnemies[i];
                Vector3 rel = enemy.transform.position - teamCenter;
                float dist = rel.magnitude;

                sensor.AddObservation(rel.x / PositionNorm);
                sensor.AddObservation(rel.z / PositionNorm);

                float distNorm = Mathf.Clamp01(dist / 600f);
                float distInv = 1f / (dist + 1f);
                sensor.AddObservation(distNorm);
                sensor.AddObservation(distInv);

                sensor.AddObservation(enemy.hp * StatScale);
                sensor.AddObservation(enemy.attack * StatScale);

                float isInf = enemy.position == LowerAgent.Position.Infantry ? 1f : 0f;
                float isArt = enemy.position == LowerAgent.Position.Artillery ? 1f : 0f;
                float isTank = enemy.position == LowerAgent.Position.Tank ? 1f : 0f;
                sensor.AddObservation(isInf);
                sensor.AddObservation(isArt);
                sensor.AddObservation(isTank);
            }
            else
            {
                for (int j = 0; j < 9; j++) sensor.AddObservation(0f);
            }
        }

        // BU 모델 호환: Space Size 90 맞추기 위한 패딩 (62 → 90)
        for (int i = 0; i < 28; i++)
            sensor.AddObservation(0f);
    }

    private Vector3 ComputeCenter<T>(List<T> agents) where T : Agent
    {
        if (agents == null || agents.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var a in agents)
        {
            if (a == null) continue;
            sum += a.transform.position;
            count++;
        }
        return count == 0 ? Vector3.zero : sum / count;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Continuous Actions는 무시 (모델 호환용으로만 존재)
        assignStepCounter++;
        if (assignStepCounter % TargetAssignInterval != 0) return;

        var aliveAllies = MyLower.FindAll(a => a != null && a.IsActive);
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);
        if (aliveEnemies.Count == 0) return;

        Vector3 teamCenter = ComputeCenter(aliveAllies);

        aliveEnemies.Sort((a, b) =>
        {
            float da = (a.transform.position - teamCenter).sqrMagnitude;
            float db = (b.transform.position - teamCenter).sqrMagnitude;
            return da.CompareTo(db);
        });

        var da = actionBuffers.DiscreteActions;
        int branches = Mathf.Min(da.Length, MyLower.Count);

        for (int i = 0; i < branches; i++)
        {
            var lower = MyLower[i];
            if (lower == null || !lower.IsActive) continue;

            int idx = Mathf.Abs(da[i]) % Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);
            lower.SetTarget(aliveEnemies[idx]);
        }
    }

    private void FixedUpdate()
    {
        stepsSinceDecision++;
    }
}
