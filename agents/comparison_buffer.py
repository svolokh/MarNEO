from collections import deque
import random
from typing import List, Any

from skimage.metrics import structural_similarity

class ComparisonBuffer(object):
    def __init__(self, capacity: int) -> None:
        self.buffer = deque([], maxlen=capacity)

    def push(self,
             state: Any) -> None:
        self.buffer.append(state)

    def sample(self, batch_size: int) -> List[Any]:
        return random.sample(self.buffer, batch_size)

    def calculate_average_similarity(self, batch_size: int, state: Any) -> float:
        sampled_states = self.sample(batch_size)
        running_sum = 0

        for existing_state in sampled_states:
            score = structural_similarity(existing_state, state, full=False)
            running_sum += score

        return running_sum / batch_size

    def empty(self) -> None:
        self.buffer.clear()

    def __len__(self):
        return len(self.buffer)