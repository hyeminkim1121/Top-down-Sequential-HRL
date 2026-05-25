# Top-down Sequential HRL for Multi-Agent Coordination

Code for the paper: **"A Sequential Training Framework for Stabilizing Hierarchical Multi-Agent Reinforcement Learning"**

## Project Structure

```
Assets/
  UpperAgent.cs                    # Upper-level PPO agent (strategic target assignment)
  LowerAgent.cs                    # Lower-level MA-POCA agent (tactical execution)
  LowerAgentTD_forStage1.cs        # Heuristic lower agent for TD Stage 1
  NewEnvController.cs              # Environment controller with TFE/FER/SCC evaluation metrics
  Scenes/
    HRL_TD_Stage1.unity            # TD Stage 1 scene (Upper trains, Lower heuristic)
    HRL_TD_Stage2.unity            # TD Stage 2 scene (Lower trains, Upper frozen)
  HRL_Baseline/
    Scenes/
      HRL_BU_Stage1.unity          # BU Stage 1 scene (Lower trains, Upper random)
      HRL_BU_Stage2.unity          # BU Stage 2 scene (Upper trains, Lower frozen)
      HRL_Concurrent.unity         # Concurrent training scene (both train simultaneously)
      Flat_MARL.unity              # Flat MA-POCA baseline scene (no hierarchy)
    Scripts/
      UpperAgentBaseline.cs        # Upper agent for BU Stage 2 (PPO learning)
      UpperAgentBU_forStage1.cs    # Heuristic upper agent for BU Stage 1 (random target)
      LowerAgentBaseline.cs        # Lower agent for BU/Concurrent experiments
      MarlAgent.cs                 # Flat MA-POCA baseline agent
      RsaAgent_RandomMove.cs       # RSA opponent with random movement (used during training)
      RsaAgent_RuleBased.cs        # RSA opponent with rule-based movement (used during evaluation)
config/
  hrl_upper_training.yaml          # TD Stage 1: Upper PPO training
  hrl_lower_training.yaml          # TD Stage 2: Lower MA-POCA training
  hrl_bu_stage1.yaml               # BU Stage 1: Lower MA-POCA training
  hrl_bu_stage2.yaml               # BU Stage 2: Upper PPO training
  hrl_concurrent.yaml              # Concurrent: Upper + Lower simultaneous training
  baseline_marl_training.yaml      # Flat MA-POCA baseline training
```

## Requirements

- Unity 2021.3+ with ML-Agents Release 20
- Python 3.8+ with PyTorch 1.13.1, CUDA 11.7
- ML-Agents Python package (`mlagents==0.29.0`)

## Training

### Sequential HRL (Top-Down) — Proposed Method
```bash
# Stage 1: Train Upper agent (Lower follows orders via heuristic)
mlagents-learn config/hrl_upper_training.yaml --run-id=td_s1 --seed=1

# Stage 2: Train Lower agents (Upper frozen with Stage 1 model)
mlagents-learn config/hrl_lower_training.yaml --run-id=td_s2 --seed=1
```

### Sequential HRL (Bottom-Up)
```bash
# Stage 1: Train Lower agents (Upper assigns random targets)
mlagents-learn config/hrl_bu_stage1.yaml --run-id=bu_s1 --seed=1

# Stage 2: Train Upper agent (Lower frozen with Stage 1 model)
mlagents-learn config/hrl_bu_stage2.yaml --run-id=bu_s2 --seed=1
```

### Concurrent HRL
```bash
mlagents-learn config/hrl_concurrent.yaml --run-id=concurrent --seed=1
```

### Flat MARL Baseline
```bash
mlagents-learn config/baseline_marl_training.yaml --run-id=flat_marl --seed=1
```

## Evaluation

The `NewEnvController.cs` includes built-in evaluation metrics:
- **TFE** (Time-to-First Engagement): elapsed steps until first attack
- **FER** (Focused Elimination Ratio): proportion of enemies eliminated within role-specific thresholds
- **SCC** (Survivor Clustering Coefficient): average pairwise distance among surviving agents

Enable evaluation by setting `enableTFE = true` and `maxEpisodes = 100` in the Inspector.
Set `experimentName` to label each experiment (e.g., "TD_vsRSA", "BU_vsRSA").

## RSA Opponents

Two rule-based opponents are provided for training and evaluation:
- **RsaAgent_RandomMove**: Random movement and random target selection. Used as the opponent during policy training.
- **RsaAgent_RuleBased**: Rule-based movement (rotate toward target, approach, shoot). Used as the opponent during evaluation to test learned policies against a competent baseline.

## License

This project builds upon Unity ML-Agents Toolkit (Apache License 2.0).
The custom scripts and configurations in this repository are released
for academic research purposes.
