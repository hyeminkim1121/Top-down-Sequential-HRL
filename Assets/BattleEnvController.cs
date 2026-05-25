using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using System.IO;

/// <summary>
/// 교전 실험용 환경 컨트롤러.
/// NewEnvController를 상속하여 LowerAgent의 EnvController 참조 호환.
/// Blue(LowerAgent) vs Red(RedLowerAgent) 교전, 승률 집계, 자동 종료.
/// </summary>
public class BattleEnvController : NewEnvController
{
    [Header("Battle Settings")]
    public BattleUpperAgent BlueUpper;
    public RedBattleUpperAgent RedUpper;
    public int battleMaxEpisodes = 100;
    public string battleName = "TDvsBU";

    private int battleEpCount = 1;
    private int blueWins, redWins, drawCount;
    private string battleWinStatus = "draw";

    private int battleBlueCount, battleRedCount;

    private List<string> battleCsvData = new();
    private string battleCsvPath;

    void Start()
    {
        string date = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        battleCsvPath = $"C:/Users/USER/Desktop/Battle_{battleName}_{date}.csv";
        battleCsvData.Add("Episode,Steps,WinStatus,BlueAlive,RedAlive");

        // 에이전트 수집
        var allAgents = FindObjectsOfType<Agent>();
        foreach (var agent in allAgents)
        {
            if (AgentList.Exists(a => a.agent == agent)) continue;
            AgentList.Add(new AgentInfo
            {
                agent = agent,
                Behavior = agent.GetComponent<BehaviorParameters>()
            });
        }

        // Blue 등록
        foreach (var info in AgentList)
        {
            if (info.agent is LowerAgent l && !(info.agent is RedLowerAgent))
            {
                BlueUpper.RegisterAgents(l, null);
                battleBlueCount++;
            }
        }

        // Red 등록
        foreach (var info in AgentList)
        {
            if (info.agent is RedLowerAgent r)
            {
                RedUpper.RegisterAgents(r, null);
                battleRedCount++;
            }
        }

        // 적 등록: Blue Upper에 Red, Red Upper에 Blue
        foreach (var info in AgentList)
        {
            if (info.agent is RedLowerAgent r)
                BlueUpper.RegisterAgents(null, r);
        }
        foreach (var info in AgentList)
        {
            if (info.agent is LowerAgent l && !(info.agent is RedLowerAgent))
                RedUpper.RegisterAgents(null, l);
        }

        // EnvController 참조 자동 세팅 (Inspector Missing 방지)
        foreach (var info in AgentList)
        {
            if (info.agent is LowerAgent l)
            {
                l.EnvController = this;
            }
        }

        CurrentBlue = battleBlueCount;
        CurrentRed = battleRedCount;

        Debug.Log($"[Battle] Blue: {battleBlueCount}, Red: {battleRedCount}");
    }

    public new void AgentShoot(Agent shooter, Agent target)
    {
        bool shooterIsBlue = shooter is LowerAgent && !(shooter is RedLowerAgent);
        bool shooterIsRed = shooter is RedLowerAgent;
        bool targetIsBlue = target is LowerAgent && !(target is RedLowerAgent);
        bool targetIsRed = target is RedLowerAgent;

        if (shooterIsBlue && targetIsRed)
        {
            LowerAgent atk = (LowerAgent)shooter;
            RedLowerAgent vic = (RedLowerAgent)target;
            if (!vic.IsActive || vic.hp <= 0f) return;

            vic.hp -= atk.attack;
            vic.hp = Mathf.Max(vic.hp, 0f);

            if (vic.hp <= 0)
            {
                vic.IsActive = false;
                vic.gameObject.SetActive(false);
                CurrentRed--;
            }
        }
        else if (shooterIsRed && targetIsBlue)
        {
            RedLowerAgent atk = (RedLowerAgent)shooter;
            LowerAgent vic = (LowerAgent)target;
            if (!vic.IsActive || vic.hp <= 0f) return;

            vic.hp -= atk.attack;
            vic.hp = Mathf.Max(vic.hp, 0f);

            if (vic.hp <= 0)
            {
                vic.IsActive = false;
                vic.gameObject.SetActive(false);
                CurrentBlue--;
            }
        }
    }

    void FixedUpdate()
    {
        if (CurrentRed <= 0)
        {
            battleWinStatus = "blue_win";
            blueWins++;
            BattleEndEpisode();
            return;
        }

        if (CurrentBlue <= 0)
        {
            battleWinStatus = "red_win";
            redWins++;
            BattleEndEpisode();
            return;
        }

        if (ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            battleWinStatus = "draw";
            drawCount++;
            BattleEndEpisode();
            return;
        }

        ResetTimer++;
    }

    private void BattleEndEpisode()
    {
        string row = $"{battleEpCount},{ResetTimer},{battleWinStatus},{CurrentBlue},{CurrentRed}";
        battleCsvData.Add(row);
        File.WriteAllLines(battleCsvPath, battleCsvData);

        Debug.Log($"[Battle] Ep{battleEpCount}: {battleWinStatus} (B:{CurrentBlue} R:{CurrentRed}) " +
                  $"전적 B{blueWins}/R{redWins}/D{drawCount}");

        if (battleEpCount >= battleMaxEpisodes)
        {
            float bWR = (float)blueWins / battleEpCount * 100f;
            float rWR = (float)redWins / battleEpCount * 100f;
            Debug.Log($"[Battle] 완료! Blue:{bWR:F1}% Red:{rWR:F1}% Draw:{drawCount}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        battleEpCount++;

        if (BlueUpper != null) BlueUpper.EndEpisode();
        if (RedUpper != null) RedUpper.EndEpisode();

        BattleResetScene();
    }

    private void BattleResetScene()
    {
        foreach (var info in AgentList)
        {
            if (info.agent is LowerAgent l)
            {
                l.hp = l.hpMax;
                l.IsActive = true;
                l.gameObject.SetActive(true);

                bool isRed = l is RedLowerAgent;
                float zMin = isRed ? 100f : -300f;
                float zMax = isRed ? 300f : -100f;
                float y = isRed ? 5.8f : 5.78f;

                Vector3 pos = new(Random.Range(-290f, 290f), y, Random.Range(zMin, zMax));
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                l.transform.SetPositionAndRotation(pos, rot);
            }
        }

        CurrentBlue = battleBlueCount;
        CurrentRed = battleRedCount;
        ResetTimer = 0;
    }
}
