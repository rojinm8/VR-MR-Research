# VR-MR-Research



\# VR-MR-Research | SocioCast



\## Project Overview



This repository documents mixed reality (MR) research conducted at the Pervasive Autonomy Lab at the University of California, Irvine (UCI).



The project focuses on developing a real-time multimodal data collection and streaming system for Meta Quest Pro headsets using Unity, C#, Python, and Flask.



\## Key Features



\- Real-time headset position and orientation tracking

\- Eye gaze tracking and validity monitoring

\- Microphone audio recording and streaming

\- Mixed reality interaction logging

\- Simultaneous streaming from multiple headsets

\- Timestamp-based data synchronization

\- Session-based data organization

\- Network throughput and message continuity analysis

\- Battery consumption evaluation



\## System Architecture



Meta Quest Pro → Unity C# Streaming Client → Wi-Fi → Python Flask Server → Research Data Storage and Analysis



The system transmits multimodal sensor data through HTTP POST requests to a centralized Python server.



\## Hardware and Software



\- Meta Quest Pro

\- Unity 2022.3.62f2

\- Meta XR SDK

\- C#

\- Python 3

\- Flask

\- Android

\- Wi-Fi



\## Sensor Configuration



| Modality | Configured Sampling Rate |

|---|---|

| Headset position | 10 Hz |

| Headset orientation | 10 Hz |

| Eye gaze | 10 Hz |

| Microphone audio | 16,000 Hz |

| Audio chunk duration | 100 ms |

| Interaction events | Event-driven |



These rates represent the configured application-level data collection and streaming settings.



\## Repository Structure



\- Unity-Project/: Initial mixed reality prototype.

\- SocioCast-Unity-Scripts/: Final research streaming and tracking scripts, selected Unity settings, and package dependencies.

\- MR\_Python\_Server/: Python server implementation and synchronization tools.



\## Main Unity Scripts



| File | Description |

|---|---|

| RealTimePoseAudioGazeStreamer.cs | Real-time multimodal sensor streaming |

| EyeGazeReader.cs | Eye gaze data acquisition |

| PlayerLocationLogger.cs | Player position logging |

| VoiceRecorder.cs | Microphone audio recording |



\## Python Server Scripts



| File | Description |

|---|---|

| live\_stream\_server.py | Main SocioCast streaming and recording server |

| server.py | Initial Flask interaction server |

| sync\_check.py | Data synchronization verification |

| requirements.txt | Python package dependencies |



\## Server Installation



Clone the repository and navigate to the Python server directory:



```bash

git clone https://github.com/rojinm8/VR-MR-Research.git

cd VR-MR-Research/MR\_Python\_Server

```



Create a Python virtual environment:



```cmd

python -m venv .venv

.venv\\Scripts\\activate

```



Install the required dependencies:



```cmd

python -m pip install -r requirements.txt

```



\## Running the Streaming Server



Select the research session and start the server using Windows Command Prompt:



```cmd

set SOCIOCAST\_SESSION=Session\_01

python live\_stream\_server.py

```



Available session configurations:



\- Session\_01

\- Session\_02

\- Session\_03



Configure the Unity streaming client with the server computer's local IPv4 address:



```text

http://SERVER\_IP:5001/stream/chunk

```



All headsets and the server computer must be connected to the same accessible local network.



Assign a unique device identifier to each headset:



\- quest\_1

\- quest\_2

\- quest\_3

\- quest\_4



\## Research Evaluation



The streaming system was evaluated across three experimental sessions using four Meta Quest Pro headsets.



| Metric | Result |

|---|---:|

| Total experimental sessions | 3 |

| Headsets | 4 |

| Received streaming messages | 140,875 |

| Observed missing sequence numbers | 39 |

| Observed sequence gap rate | Approximately 0.028% |

| Mean aggregate application throughput | Approximately 1.57 Mbps |



The evaluation examined streaming reliability, message continuity, network throughput, synchronization, and battery consumption.



\## Data Privacy and Security



Raw participant recordings, microphone audio, individual tracking datasets, signing keystores, passwords, and confidential laboratory information are excluded from this public repository.



The repository provides research implementation components and selected configuration files. Additional laboratory application assets may be necessary to reproduce the complete experimental environment.



\## Future Work



\- Evaluate different sensor sampling frequencies.

\- Investigate the trade-off between sampling rate and energy efficiency.

\- Measure CPU and GPU utilization.

\- Evaluate end-to-end streaming latency.

\- Optimize multi-headset streaming performance.

\- Improve long-duration experimental reliability.



\## Contributor



Rojin Masoumian



Electrical Engineering



University of California, Irvine



Pervasive Autonomy Lab



