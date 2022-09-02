from collections import deque
import random
from typing import List, Any

from skimage.metrics import structural_similarity
import torch

# from torchmetrics import StructuralSimilarityIndexMeasure
#from torchmetrics.image.lpip import LearnedPerceptualImagePatchSimilarity


class ComparisonBuffer(object):
    def __init__(self, capacity: int) -> None:
        self.buffer = deque([], maxlen=capacity)
        self.mse_loss = torch.nn.MSELoss()
        # self.ssim = StructuralSimilarityIndexMeasure()
        # self.lpip = LearnedPerceptualImagePatchSimilarity(net_type='vgg')

    def push(self,
             state: Any) -> None:
        self.buffer.append(state)

    def sample(self, batch_size: int) -> List[Any]:
        return random.sample(self.buffer, batch_size)

    # skimage structural_similariy (cv2 images)
    def calculate_average_similarity(self, batch_size: int, state: Any) -> float:
        sampled_states = self.sample(batch_size)
        running_sum = 0

        for existing_state in sampled_states:
            score = structural_similarity(existing_state, state, full=False)
            running_sum += score

        return running_sum / batch_size
    
    # pytorch MSELoss (tensors)
    def calculate_average_mseloss(self, batch_size: int, state: Any) -> float:
        sampled_states = self.sample(batch_size)
        running_sum = 0

        for existing_state in sampled_states:
            score = self.mse_loss(existing_state, state)
            running_sum += score

        return running_sum / batch_size


    # pytorch torchmentrics ssim loss (tensors)
    def calculate_average_ssimloss(self, batch_size: int, state: Any) -> float:
        sampled_states = self.sample(batch_size)
        running_sum = 0

        for existing_state in sampled_states:
            score = self.ssim(existing_state, state)
            running_sum += score

        return running_sum / batch_size

    # pytorch torchmentrics lpip loss (tensors)
    def calculate_average_lpiploss(self, batch_size: int, state: Any) -> float:
        sampled_states = self.sample(batch_size)
        running_sum = 0

        for existing_state in sampled_states:
            score = self.lpip(existing_state, state)
            running_sum += score

        return running_sum / batch_size

    def empty(self) -> None:
        self.buffer.clear()

    def __len__(self):
        return len(self.buffer)