import gym
from gym import spaces
from gym.envs.registration import register
import os
import subprocess
import socket
import psutil
import struct
import json
import numpy as np
import time
import traceback
from marneo_config import EMUHAWK_PATH, BIZHAWK_BASEDIR

class MarneoInstanceException(Exception):
    def __init__(self, message):
        super().__init__(message)

class MarneoInstance:
    def __init__(self, identifier, rom_path, host_addr):
        self._identifier = identifier
        self._rom_path = rom_path
        self._host_addr = host_addr
        self._process = None
        self._socket = None
        self._recvbuf = b''
        self._connected = False
        self._init_msg = None

    @staticmethod
    def _find_open_port():
        used_ports = set([conn.laddr.port for conn in psutil.net_connections() if conn.status != 'TIME_WAIT'])
        min_port = 10000
        max_port = 30000
        for port in range(min_port, max_port + 1):
            if port not in used_ports:
                return port
        raise MarneoInstanceException('could not find an open port')

    def _receive_bytes(self, n):
        while len(self._recvbuf) < n:
            remaining = n - len(self._recvbuf)
            data = self._socket.recv(remaining)
            self._recvbuf += data
        assert not len(self._recvbuf) > n
        data = self._recvbuf
        self._recvbuf = b''
        return data

    def _receive_message(self):
        blen = self._receive_bytes(4)
        msglen = struct.unpack('i', blen)[0]
        bmsg = self._receive_bytes(msglen)
        return json.loads(bmsg.decode('utf-8'))

    def _send_message(self, msg):
        s = json.dumps(msg)
        bmsg = s.encode('utf-8')
        blen = struct.pack('i', len(bmsg))
        self._socket.sendall(blen)
        self._socket.sendall(bmsg)
    
    def start(self):
        self._port = MarneoInstance._find_open_port()
        env = dict()
        env.update(os.environ)
        env['MARNEO_ID'] = self._identifier
        env['MARNEO_ADDR'] = self._host_addr
        env['MARNEO_PORT'] = str(self._port)
        self._process = subprocess.Popen([
            EMUHAWK_PATH,
            '--open-ext-tool-dll=MarNEO',
            self._rom_path
            ], env=env, cwd=BIZHAWK_BASEDIR)
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._socket.settimeout(120)

    def connect(self):
        assert self.is_started()
        if self._connected:
            print('Warning: called connect() on instance that is already connected')
            return True
        attempts = 0
        max_attempts = 10
        while attempts < max_attempts:
            try:
                self._socket.connect((self._host_addr, self._port))
                attempts = 0
                self._connected = True
                return True
            except (ConnectionRefusedError, TimeoutError):
                attempts += 1
                time.sleep(1.0)
        raise MarneoInstanceException(
            'failed to connect to MarNeo instance within {} attempts'.format(max_attempts))

    def initialize(self):
        assert self.is_started() and self.is_connected()
        if self.is_initialized():
            print('Warning: called initialize() on already initialized game instance')
            return True
        else:
            while True:
                msg = self._receive_message()
                if msg['ready']:
                    self._init_msg = msg
                    return True

    def send_action(self, action):
        self._send_message({'action': int(action)})

    def send_wait(self):
        self._send_message({'wait': True})

    def receive_state(self):
        assert self.is_connected() and self.is_initialized()
        return self._receive_message()

    def is_started(self):
        return self._process is not None

    def is_connected(self):
        return self._connected

    def is_initialized(self):
        return self._init_msg is not None

    def get_init_message(self):
        return self._init_msg

    def close(self):
        if self._socket is not None:
            self._socket.close()
        if self._process is not None:
            self._process.kill()
            try:
                self._process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                raise MarneoInstanceException('failed to terminate game process')

class MarneoEnv(gym.Env):
    metadata = {'render_modes': None}

    def __init__(self, identifier, rom_path, host_addr='127.0.0.1'):
        required_params = {'identifier', 'rom_path'}
        for param in required_params:
            if not locals()[param]:
                raise Exception('missing required parameter \'{}\''.format(param))
        self.observation_space = spaces.Box(low=0.0, high=1.0, shape=(0x100,))
        self.action_space = spaces.Discrete((0x100 - 0x1 + 1) + 1) # add 1 extra for the "null" action 0
        self._identifier = identifier
        self._rom_path = rom_path
        self._host_addr = host_addr
        self._game_inst = None

    def _parse_observation(self, observation):
        return np.array(observation, dtype=np.float32)

    def reset(self, seed=None, return_info=False, options=None):
        while True:
            try:
                if self._game_inst is not None:
                    self._game_inst.close()
                    self._game_inst = None
                self._game_inst = MarneoInstance(self._identifier, self._rom_path, self._host_addr)
                if not self._game_inst.is_started():
                    self._game_inst.start()
                if not self._game_inst.is_connected():
                    time.sleep(1.0)
                    self._game_inst.connect()
                if not self._game_inst.is_initialized():
                    self._game_inst.initialize()
                msg = self._game_inst.get_init_message()
                observation = self._parse_observation(msg['observation'])
                info = self._get_info()
                return (observation, info) if return_info else observation
            except MarneoInstanceException:
                print('Warning: encountered error when starting game, trying again')
                traceback.print_exc()

    def step(self, action):
        self._game_inst.send_action(action)
        msg = self._game_inst.receive_state()
        reward = msg['reward']
        done = msg['done']
        if not done:
            observation = self._parse_observation(msg['observation'])
        else:
            observation = np.zeros(self.observation_space.shape)
        info = self._get_info()
        info['screenshotPath'] = msg['screenshotPath']
        return observation, reward, done, info

    def close(self):
        if self._game_inst is not None:
            self._game_inst.close()
            self._game_inst = None

    def _get_info(self):
        return dict()

    def close(self):
        if self._game_inst is not None:
            self._game_inst.close()
            self._game_inst = None

register(
    id='marneo/MarneoEnv-v0',
    entry_point='marneo_env:MarneoEnv'
)