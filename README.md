# MarNEO

In traditional arcade game playing, the agent interacts with the game by simulating valid controller inputs. MarNEO is a twist on this: it is a reinforcement learning based agent for Nintendo (NES) games which can modify the game's internal memory directly to achieve its objectives! We implemented a gym environment that integrates with the BizHawk emulator to accomplish this and conducted experiments with vrious kinds of reward functions.

## Requirements

- Visual Studio 2022
- Python 3.7 (or later but pretrained versions might not work in later versions)
- A Nintendo Rom Image (NES File) for each game you want to play

*Building was only tested in Windows 10/11.*

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
