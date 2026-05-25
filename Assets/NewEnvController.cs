using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using System.IO;
using System.Linq;
using Unity.VisualScripting;

public class NewEnvController : MonoBehaviour
{
    int episodecount = 1;
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;
    public int ResetTimer = 0;

    [System.Serializable]
    public class AgentInfo
    {
        public Agent agent;
        [HideInInspector]
        public BehaviorParameters Behavior;
    }

    public List<AgentInfo> AgentList = new();
    public UpperAgent BlueUpperAgent;

    [HideInInspector] public SimpleMultiAgentGroup BlueGroup = new SimpleMultiAgentGroup();
    [HideInInspector] public SimpleMultiAgentGroup RedGroup = new SimpleMultiAgentGroup();

    [HideInInspector] int BlueAgentCount;
    [HideInInspector] int RedAgentCount;
    [HideInInspector] public int CurrentBlue;
    [HideInInspector] public int CurrentRed;
    public float MaxTargetDistance = 600f;

    public float blueUpperRewardCumulative = 0f;
    public string winstatus = "win";

    private List<string> csvData = new();
    private string csvFilePath;

    // ===== TFE 측정용 =====
    [Header("TFE Settings")]
    public bool enableTFE = true;
    public int maxEpisodes = 100;
    public string experimentName = "BottomUp";
    private int blueTTFE = -1;
    private int redTTFE = -1;
    private List<string> ttfeCsvData = new();
    private string ttfeCsvPath;

    // ===== FER 측정용 =====
    // Red(RSA) 에이전트별 첫 피격 시점 기록
    private Dictionary<Agent, int> redFirstDamagedStep = new();
    private Dictionary<Agent, bool> redEliminated = new();
    // 에피소드별 FER 집계
    private int ferTankTotal, ferTankElim;
    private int ferArtTotal, ferArtElim;
    private int ferInfTotal, ferInfElim;
    private List<string> ferCsvData = new();
    private string ferCsvPath;

    // ===== SCC (Position Log) 측정용 =====
    private List<string> posLogData = new();
    private string posLogPath;

    // ===== UpperAgent 보상 스케일 (로어 로직과 독립적으로 적용) =====
    [Header("UpperAgent reward scales")]
    [Tooltip("Blue가 적에게 준 실제 피해 1당 가점")]
    public float Upper_OnDamageDealt = 0.20f;
    [Tooltip("Blue가 받은 실제 피해 1당 감점(양수로 지정)")]
    public float Upper_OnDamageTaken = 0.50f;
    [Tooltip("적 보병/포병/탱크 처치 보너스")]
    public float Upper_KillInfantry = 40f;
    public float Upper_KillArtillery = 50f;
    public float Upper_KillTank = 60f;
    [Tooltip("에피소드 종료 승/패 보상")]
    public float Upper_WinBonus = 600f;
    public float Upper_LosePenalty = -600f;

    //  LowerAgent 리워드 추적용 변수 (기존 유지)
    private List<float> lowerEpisodeRewardTotals = new();
    private float lowerRewardA = 0f;
    private float lowerRewardB = 0f;
    private float lowerRewardC = 0f;
    private float lowerRewardD = 0f;
    private float episodePenaltyReward = 0f;

    void Start()
    {
        string date = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        csvFilePath = $"C:/Users/USER/Desktop/EpisodeData_{date}.csv";
        csvData.Add("Episode,Steps,LowerTotalReward,거리리워드,생존리워드,깎은hp리워드,깎인hp리워드,킬보상,패배패널티,WinStatus");

        if (enableTFE)
        {
            ttfeCsvPath = $"C:/Users/USER/Desktop/TTFE_{experimentName}_{date}.csv";
            ttfeCsvData.Add("Episode,TotalSteps,WinStatus,BlueTTFE,RedTTFE,BlueAlive,RedAlive");

            ferCsvPath = $"C:/Users/USER/Desktop/FER_{experimentName}_{date}.csv";
            ferCsvData.Add("Episode,TankTotal,TankElim,ArtTotal,ArtElim,InfTotal,InfElim");

            posLogPath = $"C:/Users/USER/Desktop/PositionLog_{experimentName}_{date}.csv";
            posLogData.Add("Episode,Step,AgentName,HP,X,Z");
        }

        var allAgents = FindObjectsOfType<Agent>();

        foreach (var agent in allAgents)
        {
            if (AgentList.Exists(a => a.agent == agent)) continue;

            AgentInfo agentInfo = new AgentInfo
            {
                agent = agent,
                Behavior = agent.GetComponent<BehaviorParameters>()
            };
            AgentList.Add(agentInfo);
        }

        // Upper을 그룹에 포함 (기존 로어 그룹 보상과 함께 받음)
        BlueGroup.RegisterAgent(BlueUpperAgent);

        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is LowerAgent lowerAgent)
            {
                BlueGroup.RegisterAgent(agentInfo.agent);
                BlueUpperAgent.RegisterAgents(lowerAgent, null);
                BlueAgentCount++;
            }
            else if (agentInfo.agent is MARLAgent marlAgent)
            {
                RedGroup.RegisterAgent(agentInfo.agent);
                BlueUpperAgent.RegisterAgents(null, marlAgent);
                RedAgentCount++;
            }
        }

        CurrentBlue = BlueAgentCount;
        CurrentRed = RedAgentCount;

        List<MARLAgent> redAgents = new();
        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is MARLAgent red)
            {
                redAgents.Add(red);
            }
        }
        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is LowerAgent blueLower)
            {
                blueLower.EnemyLower = redAgents;
            }
        }
    }

    private float totalShootReward = 0f;

    private float killRewardSum = 0f;
    // 누적용 변수 추가
    private float totalDamageDealt = 0f;
    private float totalDamageTaken = 0f;

    // ===== 공격/피격 이벤트 =====
    public void AgentShoot(Agent shooter, Agent target)
    {
        // TFE: 첫 공격 시점 기록
        if (enableTFE)
        {
            if (shooter is LowerAgent && target is MARLAgent && blueTTFE < 0)
            {
                blueTTFE = ResetTimer;
                Debug.Log($"[TTFE] Blue 첫 공격 at step {ResetTimer}");
            }
            else if (shooter is MARLAgent && target is LowerAgent && redTTFE < 0)
            {
                redTTFE = ResetTimer;
                Debug.Log($"[TTFE] Red 첫 공격 at step {ResetTimer}");
            }
        }

        if (shooter is LowerAgent lowerAgent && target is MARLAgent marlTarget && marlTarget.IsActive && marlTarget.hp > 0f)
        {
            // FER: Red 에이전트 첫 피격 시점 기록
            if (enableTFE && !redFirstDamagedStep.ContainsKey(target))
            {
                redFirstDamagedStep[target] = ResetTimer;
            }

            float beforeHp = marlTarget.hp;
            float damage = lowerAgent.attack;

            marlTarget.hp -= damage;
            marlTarget.hp = Mathf.Max(marlTarget.hp, 0f); // 음수 방지

            float actualDamage = beforeHp - marlTarget.hp;

            if (actualDamage > 0f)
            {
                totalDamageDealt += actualDamage;

                // (로어) 기존 그룹 보상 유지
                BlueGroup.AddGroupReward(actualDamage);

                // (어퍼) 추가 보상
                if (BlueUpperAgent != null)
                    BlueUpperAgent.AddReward(actualDamage * Upper_OnDamageDealt);

                Debug.Log($"{lowerAgent.name} 실제 데미지: {actualDamage}, Group +{actualDamage}, Upper +{actualDamage * Upper_OnDamageDealt}");
            }

            if (marlTarget.hp <= 0)
            {
                marlTarget.IsActive = false;
                marlTarget.gameObject.SetActive(false);
                CurrentRed--;

                float deathReward = marlTarget.position switch
                {
                    MARLAgent.Position.Infantry => 1500f,
                    MARLAgent.Position.Artillery => 3750f,
                    MARLAgent.Position.Tank => 6000f,
                    _ => 1000f
                };

                // (로어) 기존 그룹 보상 유지
                BlueGroup.AddGroupReward(deathReward);
                killRewardSum += deathReward;

                // (어퍼) 소보너스
                float upperKill = marlTarget.position switch
                {
                    MARLAgent.Position.Infantry => Upper_KillInfantry,
                    MARLAgent.Position.Artillery => Upper_KillArtillery,
                    MARLAgent.Position.Tank => Upper_KillTank,
                    _ => Upper_KillInfantry
                };
                if (BlueUpperAgent != null) BlueUpperAgent.AddReward(upperKill);

                Debug.Log($" {marlTarget.name} 처치됨! Group +{deathReward}, Upper +{upperKill}");

                // FER: 첫 피격 후 threshold 내 처치 여부
                if (enableTFE && redFirstDamagedStep.ContainsKey(target))
                {
                    int elapsed = ResetTimer - redFirstDamagedStep[target];
                    int threshold = marlTarget.position switch
                    {
                        MARLAgent.Position.Tank => 150,
                        MARLAgent.Position.Artillery => 180,
                        MARLAgent.Position.Infantry => 90,
                        _ => 150
                    };

                    switch (marlTarget.position)
                    {
                        case MARLAgent.Position.Tank:
                            ferTankTotal++;
                            if (elapsed < threshold) ferTankElim++;
                            break;
                        case MARLAgent.Position.Artillery:
                            ferArtTotal++;
                            if (elapsed < threshold) ferArtElim++;
                            break;
                        case MARLAgent.Position.Infantry:
                            ferInfTotal++;
                            if (elapsed < threshold) ferInfElim++;
                            break;
                    }
                    Debug.Log($"[FER] {marlTarget.name} ({marlTarget.position}): elapsed={elapsed}, threshold={threshold}, {(elapsed < threshold ? "FAST" : "SLOW")}");
                }
            }
        }
        else if (shooter is MARLAgent marl && target is LowerAgent lower && lower.IsActive && lower.hp > 0f)
        {
            float beforeHp = lower.hp;
            float damage = marl.attack;

            lower.hp -= damage;
            lower.hp = Mathf.Max(lower.hp, 0f);

            float actualDamage = beforeHp - lower.hp;

            if (actualDamage > 0f)
            {
                totalDamageTaken += actualDamage;

                // (로어) 기존 그룹 페널티 유지
                float adjustedPenalty = -actualDamage * 0.5f;
                BlueGroup.AddGroupReward(adjustedPenalty);

                // (어퍼) 추가 페널티
                if (BlueUpperAgent != null)
                    BlueUpperAgent.AddReward(-actualDamage * Upper_OnDamageTaken);

                Debug.Log($"{lower.name} 실제 피격: {actualDamage}, Group {adjustedPenalty}, Upper {-actualDamage * Upper_OnDamageTaken}");
            }

            if (lower.hp <= 0)
            {
                lower.IsActive = false;
                lower.gameObject.SetActive(false);
                CurrentBlue--;
            }
        }
    }

    public void ReportLowerAgentReward(LowerAgent agent, float rA, float rB, float total)
    {
        lowerRewardA += rA;  // 거리
        lowerRewardB += rB;  // 생존
        lowerEpisodeRewardTotals.Add(total);
    }

    private void FixedUpdate()
    {
        // 레드 에이전트 아웃오브바운즈 처리(기존 유지)
        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is MARLAgent marlAgent && marlAgent.IsActive)
            {
                Vector3 position = marlAgent.transform.position;
                if (position.x < -310f || position.x > 310f || position.z < -310f || position.z > 310f)
                {
                    marlAgent.IsActive = false;
                    marlAgent.gameObject.SetActive(false);
                    CurrentRed--;
                    Debug.Log($"{marlAgent.name} has moved out of bounds and is deactivated. Remaining Red Agents: {CurrentRed}");
                }
            }
        }

        // 레드 전멸 → 승리
        if (CurrentRed <= 0)
        {
            winstatus = "win";

            // (어퍼) 엔드 보상 추가
            if (BlueUpperAgent != null) BlueUpperAgent.AddReward(Upper_WinBonus);

            ResetScene();
            return;
        }

        // 블루 전멸 → 패배
        if (CurrentBlue <= 0)
        {
            winstatus = "lose";

            float penalty = -50000f;
            BlueGroup.AddGroupReward(penalty);
            episodePenaltyReward = penalty;

            // (어퍼) 엔드 페널티 추가
            if (BlueUpperAgent != null) BlueUpperAgent.AddReward(Upper_LosePenalty);

            Debug.Log(" Blue팀 패배! GroupReward -50000");
            ResetScene();
            return;
        }

        // 최대 스텝 초과 → 무승부
        if (ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            winstatus = "draw";

            float penalty = -5000f;
            BlueGroup.AddGroupReward(penalty);
            episodePenaltyReward = penalty;

            // (어퍼) 무승부 보상은 0으로 둠(필요 시 조절)
            Debug.Log(" 무승부! GroupReward -5000");
            ResetScene();
            return;
        }

        // SCC: 50스텝마다 Blue 에이전트 위치 로깅
        if (enableTFE && ResetTimer % 50 == 0)
        {
            foreach (var agentInfo in AgentList)
            {
                if (agentInfo.agent is LowerAgent l && l.IsActive)
                {
                    Vector3 p = l.transform.position;
                    posLogData.Add($"{episodecount},{ResetTimer},{l.name},{l.hp:F1},{p.x:F2},{p.z:F2}");
                }
            }
        }

        ResetTimer++;
    }

    private void LogEpisodeData()
    {
        float totalSum = lowerRewardA + lowerRewardB + totalDamageDealt - totalDamageTaken + episodePenaltyReward;

        Debug.Log($"[LowerAgent 보상 요약 - Ep {episodecount}] " +
          $"A (거리): {lowerRewardA:F2}, B (생존): {lowerRewardB:F2}, " +
          $"C (깎은 데미지): {totalDamageDealt:F2}, D (깎인 데미지): {-totalDamageTaken:F2}, " + // <- 음수화
          $"패널티: {episodePenaltyReward:F2}, Total: {totalSum:F2}");

        string episodeData = $"{episodecount},{ResetTimer},{totalSum:F2}," +
                     $"{lowerRewardA:F2},{lowerRewardB:F2}," +
                     $"{totalDamageDealt:F2},{-totalDamageTaken:F2}," + // <- 음수화
                     $"{killRewardSum:F2},{episodePenaltyReward:F2},{winstatus}";

        csvData.Add(episodeData);
        File.WriteAllLines(csvFilePath, csvData);

        Debug.Log($"Episode {episodecount} data saved: {episodeData}");
    }

    public void ResetScene()
    {
        // 모든 에이전트 비활성화(기존 유지)
        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is LowerAgent l)
            {
                l.ReportEpisodeRewardToController();
                l.IsActive = false;
                l.gameObject.SetActive(false);
            }
            else if (agentInfo.agent is MARLAgent r)
            {
                r.IsActive = false;
                r.gameObject.SetActive(false);
            }
        }

        LogEpisodeData();

        // TFE 로깅
        if (enableTFE)
        {
            string row = $"{episodecount},{ResetTimer},{winstatus},{blueTTFE},{redTTFE},{CurrentBlue},{CurrentRed}";
            ttfeCsvData.Add(row);
            File.WriteAllLines(ttfeCsvPath, ttfeCsvData);
            Debug.Log($"[TTFE] Ep{episodecount}: Blue={blueTTFE}, Red={redTTFE}, {winstatus}, BlueAlive={CurrentBlue}, RedAlive={CurrentRed}");

            blueTTFE = -1;
            redTTFE = -1;

            // FER 에피소드별 로깅
            string ferRow = $"{episodecount},{ferTankTotal},{ferTankElim},{ferArtTotal},{ferArtElim},{ferInfTotal},{ferInfElim}";
            ferCsvData.Add(ferRow);
            File.WriteAllLines(ferCsvPath, ferCsvData);

            redFirstDamagedStep.Clear();

            // SCC 위치 로그 저장
            File.WriteAllLines(posLogPath, posLogData);

            // 100 에피소드 도달 시 자동 종료
            if (episodecount >= maxEpisodes)
            {
                Debug.Log($"[TFE] {maxEpisodes} 에피소드 완료! 자동 종료.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
        }

        episodecount++;

        // 보상 초기화(기존 유지)
        lowerRewardA = 0f;
        lowerRewardB = 0f;
        lowerRewardC = 0f;
        lowerRewardD = 0f;
        episodePenaltyReward = 0f;
        killRewardSum = 0f;
        totalDamageDealt = 0f;
        totalDamageTaken = 0f;
        lowerEpisodeRewardTotals.Clear();

        // 그룹/어퍼 에피소드 종료(기존 유지 + Upper)
        BlueGroup.EndGroupEpisode();
        RedGroup.EndGroupEpisode();
        if (BlueUpperAgent != null) BlueUpperAgent.EndEpisode();

        CurrentBlue = BlueAgentCount;
        CurrentRed = RedAgentCount;
        blueUpperRewardCumulative = 0f;

        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is LowerAgent lowerAgent)
            {
                Vector3 pos = new(Random.Range(-290f, 290f), 5.78f, Random.Range(-300f, -100f));
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                lowerAgent.transform.SetPositionAndRotation(pos, rot);
                lowerAgent.gameObject.SetActive(true);
                lowerAgent.IsActive = true;
            }
            else if (agentInfo.agent is MARLAgent marlAgent)
            {
                Vector3 pos = new(Random.Range(-290f, 290f), 5.8f, Random.Range(100f, 300f));
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                marlAgent.transform.SetPositionAndRotation(pos, rot);
                marlAgent.gameObject.SetActive(true);
                marlAgent.IsActive = true;
            }
        }

        foreach (var agentInfo in AgentList)
        {
            if (agentInfo.agent is LowerAgent lowerAgent)
            {
                BlueGroup.RegisterAgent(lowerAgent);
            }
        }

        ResetTimer = 0;
    }
}
