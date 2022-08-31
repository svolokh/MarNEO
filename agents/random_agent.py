import gym
from gym.wrappers import TimeLimit
import json
import random
import numpy as np
import marneo_env
import sys

rom_path = sys.argv[1]
env = gym.make('marneo/MarneoEnv-v0',
    identifier='env_random',
    rom_path=rom_path,
    port=14000)
# env = TimeLimit(env, max_episode_steps=600)
try:
    obs = env.reset()
    while True:
        action = random.randint(0, env.unwrapped.action_space.n-1)
        obs, reward, done, _ = env.step(action)
        if done:
            obs = env.reset()
finally:
    env.close()