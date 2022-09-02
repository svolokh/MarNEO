# MarNEO

https://www.youtube.com/watch?v=rSbV2c8Yd84

In traditional arcade game playing, the agent interacts with the game by simulating valid controller inputs. MarNEO is a twist on this: it is a reinforcement learning based agent for Nintendo (NES) games which can modify the game's internal memory directly to achieve its objectives! We implemented a gym environment that integrates with the BizHawk emulator to accomplish this and conducted experiments with various reward functions.

## Requirements

- Visual Studio 2022
- Python 3.7 (or later but pretrained versions might not work in later versions)
- A Nintendo Rom Image (NES File) for each game you want to play

*Building was only tested in Windows 10/11.*


## Reinforcement Learning Environment
We use the BizHawk Scriptable NES Emulator with memory editing API. 
Although NES has 2048 (0x800) addressable units of memory, often the most interesting values (such as player position, level number, etc.) are the ones at the beginning. Therefore, we define the action space to be 200 actions referring to the first 200 bytes of the memory. Each action corresponds to incrementing a byte of memory at that address (we normalize the first 200 bytes of game memory to be from -1 to 1).

Our RL algorithm attempts to observe the current state of the game and successfully decide which memory address to modify. We experiment with three functions. 
Using a similarity metric, we implement a fitness function that is based on the novelty of the current game state compared to n randomly selected past states. Specifically, we use structural similarity to compute the visual resemblance of the game’s current state to 10 random previously seen rendered outputs.
We utilize a reward function based on the agent’s horizontal position. A positive reward is given when the agent moves to the right (in front).
We also train an RL agent based on a combination of the above two reward functions (novelty and horizontal position).

We exhibit the trained RL agents on different games:
- Mario Bros with reward on novelty
- Zelda with reward on novelty
- Tetris with reward on novelty
- Pac-Man with reward on novelty
- Mario Bros with reward on horizontal position
- Mario Bros with reward on horizontal position + novelty

We have also implemented a graphical user interface used for action visualization.


## Installation

```
git clone https://github.com/GameAISchool2022members/MarNEO

cd ./MarNEO/agents

pip install -r requiremnts.txt\
```

## Running

### Random Agent
```
python random_agents.py path_to_your_rom_file_.nes
```

### Pretrained Model
```
PlaceHolder command
```

## Training
```
Placeholder Training
```

## Other Experiments
- Different state space representation using an autoencoder
- Different image similarity metrics, such as Mean Square Error, Learnt Perceptual Similarity
- Identify the current score of the game based on the memory address extracted
- Assign a negative reward score if a game crash is detected as the result of a memory modification
    - Implement logic to detect when the game has crashed, e.g. by checking whether the game has been outputting the same frame for the last X seconds,
    - Assign a negative reward and just permanently blacklist an address from ever being modified if it causes a crash (use an RL implementation that supports invalid action masking, such as stable-baselines3-contrib's maskable PPO implementation)
    - Just limit the episode duration to some amount
- Directly compare bytes for the implementation of the reward function


## Future Work
- Attempt to use an evolutionary approach to operate on the byte-level.
- Use exploration bonuses like random network distillation paper.

## Ressources
- BizHawak, a multi-system emulator written in C#: https://github.com/TASEmulators/BizHawk
- RAM Map showing the data stored in a system's RAM: https://datacrystal.romhacking.net/wiki/Category:RAM_maps
  - E.g. for Mario Bros: https://datacrystal.romhacking.net/wiki/Super_Mario_Bros.:RAM_map

## Authors
Sasha Volokh 

Nemanja Rasajski

Sylvain Lapeyrade

Lefteris Ioannou

Robert Xu

Christos Davillas
