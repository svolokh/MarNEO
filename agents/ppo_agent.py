import gym
import argparse
from gym.wrappers import TimeLimit
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from torch.distributions import Distribution
import os.path
import traceback
import json
import marneo_env
from marneo_env import MarneoInstanceException

time_limit = 500
num_envs = 5

rom_path = None

class TrainingCallback(BaseCallback):
    def __init__(self, save_path, verbose=1):
        super(TrainingCallback, self).__init__(verbose)
        self.save_path = save_path
        self._last_ep = None

    def _on_step(self):
        if self.n_calls % 3000 == 0:
            self.model.save(self.save_path)
        if len(self.model.ep_info_buffer) > 0 and self.model.ep_info_buffer[-1] != self._last_ep:
            ep_info = self.model.ep_info_buffer[-1]
            self.logger.record('episode/reward', ep_info['r'])
            self.logger.dump(step=self.num_timesteps)
            self._last_ep = ep_info

def make_env(rom_path, port, is_training):
    def _init():
        env = gym.make('marneo/MarneoEnv-v0',
            identifier='env_{}'.format(port),
            rom_path=rom_path,
            port=port)        
        if is_training:
            env = TimeLimit(env, max_episode_steps=time_limit)
        return env
    return _init

if __name__ == '__main__':
    Distribution.set_default_validate_args(False)

    parser = argparse.ArgumentParser()
    parser.add_argument('rom_path', metavar='P')
    parser.add_argument('--predict', dest='is_predict', default=False, action='store_true')
    args = parser.parse_args()

    rom_path = args.rom_path

    checkpoint_save_path = os.path.join(os.getcwd(), 'checkpoint')
    while True:
        start_port = 12000
        model_args = dict(policy_kwargs=dict(net_arch=[128, 128]),
                          n_steps=time_limit,
                          verbose=1)
        if args.is_predict:
            nenvs = 1
            env = make_env(rom_path, start_port, False)()
        else:
            dbg_env = False
            if dbg_env:
                nenvs = 1
                env = make_env(rom_path, start_port, True)()
            else:
                nenvs = num_envs
                env = VecMonitor(SubprocVecEnv([make_env(rom_path, start_port + i, True) for i in range(nenvs)]))
                model_args['tensorboard_log'] = './tboard_results'
        
        chkpt_path = checkpoint_save_path + '.zip'
        if os.path.exists(chkpt_path):
            print('Loading existing model from {}'.format(chkpt_path))
            model = PPO.load(chkpt_path, env, custom_objects={'n_envs': num_envs}, **model_args)
        else:
            model = PPO('MlpPolicy', env, **model_args)

        if args.is_predict:
            obs = env.reset()
            while True:
                action, _ = model.predict(obs)
                obs, reward, done, info = env.step(action)
                if done:
                    break
            env.close()
            break
        else:
            try:
                model.learn(5000 * time_limit, callback=TrainingCallback(checkpoint_save_path), tb_log_name='MarNEO_PPO', reset_num_timesteps=False)
                model.save(checkpoint_save_path)
                env.close()
                break
            except (EOFError, MarneoInstanceException) as e:
                print('Error: Encountered exception of type {} with message "{}", restarting training from checkpoint'.format(type(e), e))
                model.save(checkpoint_save_path)
                try:
                    env.close()
                except Exception:
                    print("Warning: Failed to close environment:")
                    traceback.print_exc()
                continue