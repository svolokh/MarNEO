from collections import deque
import random
from typing import List, Any
from comparison_buffer import ComparisonBuffer
from novelty_bonus import NoveltyBonus
import cv2 
import gym
import os
from PIL import Image
from skimage.metrics import structural_similarity
from pytorch_msssim import ssim, ms_ssim, SSIM, MS_SSIM
import kornia
from torchmetrics.image.lpip import LearnedPerceptualImagePatchSimilarity

import timeit
from ipdb import iex

from torchvision import transforms
import torch
from torchmetrics import StructuralSimilarityIndexMeasure
from torchmetrics.image.lpip import LearnedPerceptualImagePatchSimilarity


# pytorch MSELoss (tensors)
def calculate_mseloss(img_a, img_b):
    mse_loss = torch.nn.MSELoss()
    score = mse_loss(img_a, img_b)
    return score


# pytorch torchmentrics ssim loss (tensors)
def calculate_ssimloss(img_a, img_b):
    ssim = StructuralSimilarityIndexMeasure()
    score = ssim(img_a, img_b)
    return score

# pytorch torchmentrics lpip loss (tensors)
def calculate_lpiploss(img_a, img_b):
    lpip = LearnedPerceptualImagePatchSimilarity(net_type='vgg')
    score = lpip(img_a, img_b)
    return score
    

@iex
def main():
    # rom_path = 'test' # sys.argv[1]
    # env = gym.make('marneo/MarneoEnv-v0',
    #     identifier='env_random',
    #     port_range=(12000, 17000),
    #     rom_path=rom_path,
    #     is_training=False)
    # env = NoveltyBonus(env)
    comparisonBuffer = ComparisonBuffer(5)

    transform = transforms.Compose([transforms.Resize((256,224)),
                                    transforms.ToTensor()])#,
                                    # transforms.Normalize(mean=0., std=1.0, inplace=True)])
                                
    scrPath = '../a_few_screenshots'
    filelist=(os.listdir('../a_few_screenshots'))
    for file in filelist[:]: # filelist[:] makes a copy of filelist.
        if not(file.endswith(".jpg")):
            filelist.remove(file)
        else:
            print('FILE: ----' , file)
            # img = transform(Image.open(scrPath + '/' + file).convert('RGB')).unsqueeze(0)
            img = transform(Image.open(scrPath + '/' + file).convert('RGB')).unsqueeze(0)

            print(img.size())
            comparisonBuffer.push(img)

    # image = cv2.imread('../a_few_screenshots/66.jpg')
    # image = transform(Image.open('../a_few_screenshots/66.jpg').convert('RGB')).unsqueeze(0)
    # # similarity = comparisonBuffer.calculate_average_ssimloss(3, image)
    # similarity = comparisonBuffer.calculate_average_lpiploss(3, image)
    # print(similarity)

    image_a = transform(Image.open('../a_few_screenshots/66.jpg').convert('RGB')).unsqueeze(0)
    image_b = transform(Image.open('../a_few_screenshots/314.jpg').convert('RGB')).unsqueeze(0)

    # ssim is faster than lp
    starttime = timeit.default_timer()
    # print('mse loss: ', calculate_mseloss(image_a, image_b))
    print('ssim_loss: ', 1 - calculate_ssimloss(image_a, image_b))
    # print('lpip loss: ', calculate_lpiploss(image_a, image_b))
    print("Time taken :", timeit.default_timer() - starttime)





if __name__ == "__main__":
    main()