# Top-down Sequential HRL for Multi-Agent Coordination

Code for the paper: **"A Sequential Training Framework for Stabilizing Hierarchical Multi-Agent Reinforcement Learning"**

## Project Structure

```
Assets/
  UpperAgent.cs          # Upper-level PPO agent (strategic target assignment)
  LowerAgent.cs          # Lower-level MA-POCA agent (tactical execution)
  NewEnvController.cs    # Environment controller with TFE/FER/SCC metrics
  MarlAgent.cs           # MA-POCA baseline agent
  LowerAgentTD.cs        # Heuristic lower agent for TD Stage 1
  HRL_Ablation/
    Scripts/
      UpperAgentAblation.cs    # Upper agent for BU experiments
      LowerAgentAblation.cs    # Lower agent for BU experiments
      RsaAgent.cs              # Rule-based RSA opponent
      RsaAgentHeuristicMove.cs # RSA with heuristic movement
config/
  hrl_upper_training.yaml      # TD Stage 1: Upper PPO training
  hrl_lower_training.yaml      # TD Stage 2: Lower MA-POCA training
  baseline_marl_training.yaml  # Flat MA-POCA baseline training
```

## Requirements

- Unity 2021.3+ with ML-Agents Release 20
- Python 3.8+ with PyTorch 1.13.1, CUDA 11.7
- ML-Agents Python package (`mlagents==0.29.0`)

## Training

### Sequential HRL (Top-Down)
```bash
# Stage 1: Train Upper agent
mlagents-learn config/hrl_upper_training.yaml --run-id=td_upper --seed=1

# Stage 2: Train Lower agents (freeze Upper)
mlagents-learn config/hrl_lower_training.yaml --run-id=td_lower --seed=1
```

### Flat MARL Baseline
```bash
mlagents-learn config/baseline_marl_training.yaml --run-id=flat_marl --seed=1
```

## License

This project is released for academic research purposes.
