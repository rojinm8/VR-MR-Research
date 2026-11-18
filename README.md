
# VR-MR-Research | SocioCast

## Overview

This repository presents mixed reality (MR) research conducted at the Pervasive Autonomy Lab at the University of California, Irvine (UCI).

The project focuses on real-time multimodal data acquisition, wireless transmission, and synchronized recording using Meta Quest Pro headsets, Unity, C#, and a centralized Python Flask server.

The system was developed to support collaborative mixed reality experiments while collecting headset position, orientation, eye gaze, and microphone audio.

## Key Features

- Real-time headset position and orientation tracking
- Eye gaze tracking and validity monitoring
- Microphone audio acquisition and streaming
- Mixed reality interaction logging
- Simultaneous multi-headset streaming
- Timestamp-based data synchronization
- Session-based data organization
- Message continuity and network throughput analysis
- Battery consumption evaluation

## System Architecture

Meta Quest Pro → Unity C# Streaming Client → Wi-Fi → Python Flask Server → Data Storage and Analysis

Each headset transmits multimodal sensor data to a centralized server through HTTP POST requests. Unique device identifiers allow the server to distinguish and organize data received from multiple headsets.

## Hardware and Software

- Meta Quest Pro
- Unity 2022.3.62f2
- Meta XR SDK
- C#
- Python 3
- Flask
- Android
- Wi-Fi

## Sensor and Streaming Configuration

| Modality | Configured Sampling Rate |
|---|---:|
| Headset position | 10 Hz |
| Headset orientation | 10 Hz |
| Eye gaze | 10 Hz |
| Microphone audio | 16,000 Hz |
| Audio chunk transmission | 10 Hz nominal |
| Audio chunk duration | 100 ms |
| Interaction events | Event-driven |

Audio is represented as mono, 16-bit PCM data. The sampling frequencies represent application-level settings rather than the native hardware sensor frequencies.

## Repository Structure

- `MR_Python_Server/` - Python streaming server, synchronization tools, and dependencies.
- `SocioCast-Unity-Scripts/` - Final Unity research scripts, selected project settings, and package dependencies.
- `Unity-Project/` - Initial mixed reality prototype.
- `README.md` - Project documentation.
- `.gitignore` - Repository exclusion rules.

## Unity Implementation

| Script | Description |
|---|---|
| RealTimePoseAudioGazeStreamer.cs | Real-time position, orientation, gaze, and audio streaming |
| EyeGazeReader.cs | Eye gaze acquisition |
| PlayerLocationLogger.cs | Player position logging |
| VoiceRecorder.cs | Microphone audio recording |

The repository also preserves earlier prototype scripts for interaction logging, local position recording, audio recording, and network transmission.

## Python Implementation

| Script | Description |
|---|---|
| live_stream_server.py | Main multimodal streaming server |
| server.py | Initial Flask interaction server |
| sync_check.py | Synchronization verification |
| requirements.txt | Python dependencies |

## Installation

Clone the repository:

```bash
git clone https://github.com/rojinm8/VR-MR-Research.git
cd VR-MR-Research/MR_Python_Server
```

Create a virtual environment using Windows Command Prompt:

```cmd
python -m venv .venv
.venv\Scripts\activate
```

Install the required Python dependencies:

```cmd
python -m pip install -r requirements.txt
```

## Running the Server

Select the desired experimental session:

```cmd
set SOCIOCAST_SESSION=Session_01
```

Start the streaming server:

```cmd
python live_stream_server.py
```

The implemented research session identifiers are:

- Session_01
- Session_02
- Session_03

## Unity and Headset Configuration

Configure the Unity streaming component with the local IPv4 address of the server computer.

```text
http://SERVER_IP:5001/stream/chunk
```

Replace SERVER_IP with the server computer's actual local IPv4 address.

Connect all headsets and the server computer to the same accessible local network.

Assign each headset a unique device identifier:

- quest_1
- quest_2
- quest_3
- quest_4

The Unity application requires microphone and eye tracking permissions and the appropriate Meta XR configuration.

## Experimental Results

The streaming system was evaluated using four Meta Quest Pro headsets across three experimental sessions.

| Session | Received Messages | Observed Missing Sequence Numbers |
|---|---:|---:|
| Session 01 | 35,820 | 28 |
| Session 02 | 80,769 | 11 |
| Session 03 | 24,286 | 0 |
| Total | 140,875 | 39 |

### Network Performance

- Total received messages: 140,875
- Observed sequence gap rate: approximately 0.028%
- Mean aggregate application-level throughput: approximately 1.57 Mbps

Throughput was calculated from application-level message sizes and does not represent total physical network traffic.

Sequence gaps indicate missing application message identifiers and should not be interpreted as a direct measurement of network packet loss.

### Battery Consumption

Battery consumption was measured during the second and third experimental sessions.

| Session | Measurement Duration | Average Battery Decrease |
|---|---:|---:|
| Session 02 | 32 minutes | 25.25 percentage points |
| Session 03 | 13 minutes | 13.75 percentage points |

These measurements represent overall headset battery consumption during the experiments. They do not isolate the energy cost of streaming from other application activities.

## Reproducibility and Limitations

This repository contains the core streaming implementation, selected configuration files, and Python dependencies.

The final SocioCast scripts are provided as implementation components rather than a complete standalone Unity application. Full reproduction of the original research environment may require additional authorized laboratory assets, scenes, SDK components, and application configuration.

## Data Privacy and Security

Raw participant recordings, individual tracking datasets, microphone audio, signing credentials, and confidential laboratory information are intentionally excluded from the public repository.

The experimental server is intended for use on a controlled local network. Additional authentication and encrypted transport would be necessary for deployment on an untrusted network.

## Future Work

- Evaluate different sensor sampling frequencies.
- Investigate the trade-off between sampling frequency and energy efficiency.
- Evaluate CPU and GPU utilization.
- Measure synchronized end-to-end streaming latency.
- Improve streaming scalability and reliability.
- Optimize battery consumption during extended experiments.
- Investigate adaptive sensor sampling and transmission strategies.

## Contributor

Rojin Masoumian

Electrical Engineering

University of California, Irvine (UCI)

Pervasive Autonomy Lab
