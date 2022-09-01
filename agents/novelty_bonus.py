import gym
import cv2
from comparison_buffer import ComparisonBuffer

class NoveltyBonus(gym.Wrapper):
    def __init__(self, env):
        super().__init__(env)
        self.cmp_buff = ComparisonBuffer(100)

    def step(self, action):
        obs, reward, done, info = self.env.step(action)
        scrPath = info['screenshotPath']
        image = cv2.imread(scrPath, 0)
        self.cmp_buff.push(image)
        bonus = 0
        if len(self.cmp_buff) > 20:
            similarity = self.cmp_buff.calculate_average_similarity(10, image)
            bonus = 1 - similarity
        return obs, reward + bonus, done, info